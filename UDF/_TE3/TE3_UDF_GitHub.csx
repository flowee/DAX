// TE3 Macro: TE3 GitHub UDF Manager
//    https://github.com/avatorl/DAX/tree/master/UDF/_TE3
// ===========================================================================
// Andrzej Leszkiewicz
//    https://powerofbi.org/
//    https://www.linkedin.com/in/avatorl/
// ===========================================================================

var repoApiUrl = "https://api.github.com/repos/avatorl/DAX/contents/UDF";
var owner  = "avatorl";
var repo   = "DAX";
var branch = "master";    // or "main"

// ---------------------------------------------------------------------
// Token
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.Machine);
if (string.IsNullOrEmpty(githubToken))
{
    System.Windows.Forms.MessageBox.Show("⚠️ Missing GitHub token. Set GITHUB_TOKEN as environment variable.");
    return;
}

// ---------------------------------------------------------------------
// Normalization: only fix line endings + remove leading blank lines
Func<string, string> normalizeLineEndings = text =>
{
    if (string.IsNullOrEmpty(text)) return "";

    var norm = text.Replace("\r\n", "\n").Replace("\r", "\n");

    // Remove leading blank lines
    norm = norm.TrimStart('\n');

    // Trim trailing spaces at line ends
    norm = string.Join("\n", norm.Split('\n')
        .Select(line => line.TrimEnd()));

    // Ensure single trailing newline
    if (!norm.EndsWith("\n"))
        norm += "\n";

    return norm;
};


// ---------------------------------------------------------------------
// Download file from GitHub
Func<string,string> downloadFile = (url) =>
{
    using (var client = new System.Net.Http.HttpClient())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
        if (!string.IsNullOrEmpty(githubToken))
            client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
        return client.GetStringAsync(url).Result;
    }
};

// ---------------------------------------------------------------------
// Scan repo for .dax files
Func<string,string,System.Collections.Generic.List<dynamic>> getFuncs = null;
getFuncs = (apiUrl, relPath) =>
{
    var list = new System.Collections.Generic.List<dynamic>();
    using (var client = new System.Net.Http.HttpClient())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
        if (!string.IsNullOrEmpty(githubToken))
            client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");

        var response = client.GetStringAsync(apiUrl).Result;
        var items = Newtonsoft.Json.Linq.JArray.Parse(response);

        foreach (var item in items)
        {
            var type = item["type"].ToString();
            var name = item["name"].ToString();

            if (type == "dir")
            {
                list.AddRange(getFuncs(item["url"].ToString(),
                                       System.IO.Path.Combine(relPath, name)));
            }
            else if (type == "file" && name.EndsWith(".dax"))
            {
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
// Upload to GitHub
Action<string,string,string> uploadToGitHub = (path, code, sha) =>
{
    using (var client = new System.Net.Http.HttpClient())
    {
        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
        client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");

        var normalized = normalizeLineEndings(code);

        string url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
        var body = new {
            message = "Update UDF from Tabular Editor",
            content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(normalized)),
            branch  = branch,
            sha     = sha   // required for update
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var resp = client.PutAsync(url, new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")).Result;
        var respText = resp.Content.ReadAsStringAsync().Result;

        if (!resp.IsSuccessStatusCode)
            throw new Exception(resp.StatusCode + ": " + respText);
    }
};

// ---------------------------------------------------------------------
// Load all functions
var funcs = getFuncs(repoApiUrl, "");
if (funcs.Count == 0) { System.Windows.Forms.MessageBox.Show("No DAX functions found."); return; }

var existing = new System.Collections.Generic.HashSet<string>(
    Model.Functions.Select(f => f.Name),
    StringComparer.OrdinalIgnoreCase
);

// ---------------------------------------------------------------------
// UI
var form = new System.Windows.Forms.Form();
form.Text = "TE3 GitHub UDF Manager";
form.Width = 700; 
form.Height = 680;

var tree = new System.Windows.Forms.TreeView();
tree.CheckBoxes = true;
tree.Dock = System.Windows.Forms.DockStyle.Top;
tree.Height = 540;

foreach (var grp in funcs.GroupBy(f => f.Path).OrderBy(g => g.Key))
{
    var folderName = string.IsNullOrEmpty(grp.Key) 
        ? $"{owner}/{repo}/UDF"
        : grp.Key;

    var folder = new System.Windows.Forms.TreeNode(folderName);

    foreach (var f in grp)
    {
        var node = new System.Windows.Forms.TreeNode(f.Name);
        node.Tag = f;
        if (existing.Contains(f.Name))
            node.NodeFont = new System.Drawing.Font(tree.Font, System.Drawing.FontStyle.Bold);
        folder.Nodes.Add(node);
    }
    tree.Nodes.Add(folder);
}
tree.ExpandAll();

// ---------------------------------------------------------------------
// Buttons
var btnWidth = 180;
var updateModel = new System.Windows.Forms.Button{ Text="Update in the Model", Width=btnWidth, Top=550, Left=250, DialogResult=System.Windows.Forms.DialogResult.OK };
var updateGitHub = new System.Windows.Forms.Button{ Text="Update in GitHub", Width=btnWidth, Top=550, Left=450 };
var compare = new System.Windows.Forms.Button{ Text="Compare", Width=btnWidth, Top=550, Left=50 };
var cancel = new System.Windows.Forms.Button{ Text="Cancel", Width=btnWidth, Top=590, Left=450, DialogResult=System.Windows.Forms.DialogResult.Cancel };

form.Controls.Add(tree);
form.Controls.Add(updateModel);
form.Controls.Add(updateGitHub);
form.Controls.Add(compare);
form.Controls.Add(cancel);
form.AcceptButton = updateModel;
form.CancelButton = cancel;

// ---------------------------------------------------------------------
// Compare logic
compare.Click += (s,e) => {
    var existingNow = new System.Collections.Generic.HashSet<string>(
        Model.Functions.Select(fn => fn.Name),
        StringComparer.OrdinalIgnoreCase
    );

    foreach (System.Windows.Forms.TreeNode folder in tree.Nodes)
    {
        foreach (System.Windows.Forms.TreeNode node in folder.Nodes)
        {
            node.ForeColor = System.Drawing.Color.Black;
            node.ToolTipText = "";

            if (node.Tag == null) continue;

            dynamic f = node.Tag;
            if (existingNow.Contains((string)f.Name))
            {
                try
                {
                    // Build GitHub API URL (branch aware)
                    var path = string.IsNullOrEmpty(f.Path)
                        ? $"UDF/{f.Name}.dax"
                        : $"UDF/{f.Path.Replace("\\", "/")}/{f.Name}.dax";

                    string code;
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
                        if (!string.IsNullOrEmpty(githubToken))
                            client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");

                        string url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";
                        var json = client.GetStringAsync(url).Result;
                        var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                        var base64 = (string)obj["content"];
                        code = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                    }

                    var modelFn = Model.Functions
                        .FirstOrDefault(fn => fn.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase));

                    if (modelFn != null)
                    {
                        var repoCode  = normalizeLineEndings(code);
                        var modelCode = normalizeLineEndings(modelFn.Expression);

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
                catch (Exception ex)
                {
                    node.ForeColor = System.Drawing.Color.DarkOrange;
                    node.ToolTipText = $"Error comparing: {ex.Message}";
                }
            }
        }
    }
};

// ---------------------------------------------------------------------
// GitHub update logic
updateGitHub.Click += (s,e) => {
    var selected = new System.Collections.Generic.List<dynamic>();
    foreach (System.Windows.Forms.TreeNode folder in tree.Nodes)
        foreach (System.Windows.Forms.TreeNode node in folder.Nodes)
            if (node.Checked && node.Tag != null)
                selected.Add((dynamic)node.Tag);

    foreach (var f in selected)
    {
        try
        {
            var path = string.IsNullOrEmpty(f.Path) 
                ? $"UDF/{f.Name}.dax" 
                : $"UDF/{f.Path.Replace("\\", "/")}/{f.Name}.dax";

            // get latest SHA
            string sha = null;
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
                client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
                var resp = client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}").Result;
                if (resp.IsSuccessStatusCode)
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                    sha = (string)obj["sha"];
                }
            }

            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            if (fn != null)
            {
                // Normalize exactly the same way for upload
                var normalized = normalizeLineEndings(fn.Expression);

                // Upload
                uploadToGitHub(path, normalized, sha);

                // Set the model expression to the *same normalized text* we just pushed
                fn.Expression = normalized;
            }

        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error updating {f.Name}: {ex.Message}");
        }
    }

    System.Windows.Forms.MessageBox.Show($"Updated {selected.Count} UDF(s) in GitHub.");
};

// ---------------------------------------------------------------------
// Run dialog
if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
{
    var selected = new System.Collections.Generic.List<dynamic>();
    foreach (System.Windows.Forms.TreeNode folder in tree.Nodes)
        foreach (System.Windows.Forms.TreeNode node in folder.Nodes)
            if (node.Checked && node.Tag != null)
                selected.Add((dynamic)node.Tag);

    foreach (var f in selected)
    {
        var dax = downloadFile(f.Url);
        if (!string.IsNullOrWhiteSpace(dax))
        {
            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            var normalized = normalizeLineEndings(dax);
            if (fn == null) Model.AddFunction(f.Name, normalized);
            else fn.Expression = normalized;
        }
    }
    System.Windows.Forms.MessageBox.Show($"Loaded {selected.Count} UDF(s) into the model.");
}
