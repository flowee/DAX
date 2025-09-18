compare.Click += (s, e) =>
{
    WithWaitCursor(form, () =>
    {
        var existingNow = new System.Collections.Generic.HashSet<string>(
            Model.Functions.Select(fn => fn.Name),
            System.StringComparer.OrdinalIgnoreCase
        );

        // Cache repo file contents during compare
        var contentCache = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        TraverseNodes(tree.Nodes, node =>
        {
            // Reset defaults for every node before coloring
            node.ForeColor = System.Drawing.Color.Black;
            node.ToolTipText = "";

            if (node.Tag == null) return;

            // Skip model-only UDFs (keep them blue as set in RefreshTree)
            if (node.Tag is TabularEditor.TOMWrapper.Function)
                return;

            var f = (dynamic)node.Tag;

            // Only compare repo UDFs that also exist in model
            if (existingNow.Contains((string)f.Name))
            {
                try
                {
                    var apiPath = GetFilePath(f);
                    string code;

                    // Use cache to avoid repeated GitHub requests
                    if (!contentCache.TryGetValue(apiPath, out code))
                    {
                        var json = client.GetStringAsync(GetApiUrl(apiPath)).Result;
                        var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                        var base64 = (string)obj["content"];
                        code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(base64));
                        contentCache[apiPath] = code;
                    }

                    // Find corresponding model function
                    var modelFn = Model.Functions
                        .FirstOrDefault(fn => fn.Name.Equals(f.Name, System.StringComparison.OrdinalIgnoreCase));

                    if (modelFn != null)
                    {
                        var repoCode  = NormalizeLineEndings(code);
                        var modelCode = NormalizeLineEndings(modelFn.Expression);

                        if (modelCode == repoCode)
                        {
                            node.ForeColor = System.Drawing.Color.Green;
                            node.ToolTipText = "Match between model and GitHub";
                        }
                        else
                        {
                            node.ForeColor = System.Drawing.Color.Red;
                            node.ToolTipText = "Code differs between model and GitHub";
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    node.ForeColor = System.Drawing.Color.DarkOrange;
                    node.ToolTipText = $"Error comparing: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine("Compare error: " + ex);
                }
            }
        });
    });
};
