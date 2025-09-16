// TE3 Macro: Load DAX UDFs from GitHub and insert/update in the current model
// ===========================================================================
// Andrzej Leszkiewicz
//    https://powerofbi.org/
//    https://www.linkedin.com/in/avatorl/
// ===========================================================================

// Root folder in GitHub repo (scanned recursively for .dax files)
var repoApiUrl = "https://api.github.com/repos/avatorl/DAX/contents/UDF";

// ---------------------------------------------------------------------
// Helper: download a file (raw .dax content) from GitHub
Func<string,string> downloadFile = (url) =>
{
    using (var client = new System.Net.Http.HttpClient())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3"); // required by GitHub API
        return client.GetStringAsync(url).Result;
    }
};

// ---------------------------------------------------------------------
// Helper: recursively scan GitHub repo folders for .dax files
// Returns a list of anonymous objects: { Name, Url, Path }
Func<string,string,System.Collections.Generic.List<dynamic>> getFuncs = null;
getFuncs = (apiUrl, relPath) =>
{
    var list = new System.Collections.Generic.List<dynamic>();
    using (var client = new System.Net.Http.HttpClient())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
        var response = client.GetStringAsync(apiUrl).Result;
        var items = Newtonsoft.Json.Linq.JArray.Parse(response);

        foreach (var item in items)
        {
            var type = item["type"].ToString();
            var name = item["name"].ToString();

            if (type == "dir")
            {
                // Recurse into subfolder
                list.AddRange(getFuncs(item["url"].ToString(),
                                       System.IO.Path.Combine(relPath, name)));
            }
            else if (type == "file" && name.EndsWith(".dax"))
            {
                // Found a .dax file -> add to list
                list.Add(new { 
                    Name = System.IO.Path.GetFileNameWithoutExtension(name),
                    Url  = item["download_url"].ToString(),
                    Path = relPath
                });
            }
        }
    }
    return list;
};

// ---------------------------------------------------------------------
// Load all functions from GitHub
var funcs = getFuncs(repoApiUrl, "");
if (funcs.Count == 0) 
{
    System.Windows.Forms.MessageBox.Show("No DAX functions found.");
    return;
}

// Collect names of functions already existing in the model
var existing = new System.Collections.Generic.HashSet<string>(
    Model.Functions.Select(f => f.Name),
    System.StringComparer.OrdinalIgnoreCase
);

// ---------------------------------------------------------------------
// Build UI
var form = new System.Windows.Forms.Form();
form.Text = "Select DAX UDFs to load";
form.Width = 600; 
form.Height = 650;

var tree = new System.Windows.Forms.TreeView();
tree.CheckBoxes = true;
tree.Dock = System.Windows.Forms.DockStyle.Top;
tree.Height = 520;

// Build folder tree structure from funcs
foreach (var grp in funcs.GroupBy(f => f.Path).OrderBy(g => g.Key))
{
    var folder = new System.Windows.Forms.TreeNode(string.IsNullOrEmpty(grp.Key) ? "(root)" : grp.Key);

    foreach (var f in grp)
    {
        var node = new System.Windows.Forms.TreeNode(f.Name);
        node.Tag = f;

        // Bold existing functions
        if (existing.Contains(f.Name))
            node.NodeFont = new System.Drawing.Font(tree.Font, System.Drawing.FontStyle.Bold);

        folder.Nodes.Add(node);
    }

    tree.Nodes.Add(folder);
}
tree.ExpandAll();

// Buttons
var ok = new System.Windows.Forms.Button{ 
    Text="Update", 
    Top=540, Left=220, 
    DialogResult=System.Windows.Forms.DialogResult.OK 
};
var cancel = new System.Windows.Forms.Button{ 
    Text="Cancel", 
    Top=540, Left=430, 
    DialogResult=System.Windows.Forms.DialogResult.Cancel 
};
var compare = new System.Windows.Forms.Button{ 
    Text="Compare", 
    Top=540, Left=80 
};

// Add controls
form.Controls.Add(tree);
form.Controls.Add(ok);
form.Controls.Add(cancel);
form.Controls.Add(compare);
form.AcceptButton = ok;
form.CancelButton = cancel;

// ---------------------------------------------------------------------
// Compare button logic: check GitHub vs Model code
compare.Click += (s,e) => {
    foreach (System.Windows.Forms.TreeNode folder in tree.Nodes)
    {
        foreach (System.Windows.Forms.TreeNode node in folder.Nodes)
        {
            dynamic f = node.Tag;
            if (existing.Contains((string)f.Name))
            {
                var code = downloadFile(f.Url);
                var modelFn = Model.Functions
                    .FirstOrDefault(fn => fn.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase));

                if (modelFn != null)
                {
                    // Normalize line endings before compare
                    var repoCode = code.Trim().Replace("\r\n","\n");
                    var modelCode = modelFn.Expression.Trim().Replace("\r\n","\n");

                    if (modelCode == repoCode)
                        node.ForeColor = System.Drawing.Color.Green;   // same
                    else
                        node.ForeColor = System.Drawing.Color.Red;     // different
                }
            }
        }
    }
};

// ---------------------------------------------------------------------
// Run dialog
if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
{
    // Collect checked functions
    var selected = new System.Collections.Generic.List<dynamic>();
    foreach (System.Windows.Forms.TreeNode folder in tree.Nodes)
        foreach (System.Windows.Forms.TreeNode node in folder.Nodes)
            if (node.Checked && node.Tag != null)
                selected.Add((dynamic)node.Tag);

    // Insert or update in the model
    foreach (var f in selected)
    {
        var dax = downloadFile(f.Url);
        if (!string.IsNullOrWhiteSpace(dax))
        {
            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            if (fn == null) 
                fn = Model.AddFunction(f.Name, dax);
            else 
                fn.Expression = dax; // overwrite existing
        }
    }

    System.Windows.Forms.MessageBox.Show($"Loaded {selected.Count} UDFs.");
}
