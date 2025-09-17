// ===========================================================================
// TE3 Macro: GitHub DAX UDF Manager for TE3
// ---------------------------------------------------------------------------
// Purpose:
//   - Manage DAX user-defined functions (UDFs) between Tabular Editor 3 and a
//     GitHub repository.
//   - Features:
//       * Load DAX UDFs from GitHub into the current model.
//       * Compare model functions with GitHub repo versions.
//       * Update GitHub with selected model functions.
//       * Visual status indicators in a TreeView (normal, bold, green, red).
//
// Requirements:
//   - A GitHub personal access token (PAT) stored in the environment variable
//     GITHUB_TOKEN (either User or Machine scope).
//
// Author: Andrzej Leszkiewicz
// Links:
//   - Project repo: https://github.com/avatorl/DAX/tree/master/UDF/_TE3
//   - Blog: https://powerofbi.org/
//   - LinkedIn: https://www.linkedin.com/in/avatorl/
// ===========================================================================


// ===========================================================================
// Repository configuration
// ===========================================================================
var repoApiUrl = "https://api.github.com/repos/avatorl/DAX/contents/UDF";
var owner  = "avatorl";
var repo   = "DAX";
var branch = "master";


// ===========================================================================
// GitHub Token (required)
// ===========================================================================
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.Machine);

if (string.IsNullOrEmpty(githubToken))
{
    System.Windows.Forms.MessageBox.Show("⚠️ Missing GitHub token. Please set GITHUB_TOKEN as an environment variable.");
    return;
}


// ===========================================================================
// HttpClient (shared instance for all requests)
// ===========================================================================
var client = new System.Net.Http.HttpClient();
client.DefaultRequestHeaders.Clear();
client.DefaultRequestHeaders.Add("User-Agent", "TabularEditor3");
if (!string.IsNullOrEmpty(githubToken))
    client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");


// ===========================================================================
// Helper: Normalize line endings
// Ensures consistent formatting of UDF code before comparing/uploading
// ===========================================================================
Func<string, string> NormalizeLineEndings = text =>
{
    if (string.IsNullOrEmpty(text)) return "";

    var norm = text.Replace("\r\n", "\n").Replace("\r", "\n");
    norm = norm.TrimStart('\n'); 
    norm = string.Join("\n", norm.Split('\n').Select(line => line.TrimEnd())); 
    if (!norm.EndsWith("\n")) norm += "\n"; 
    return norm;
};


// ===========================================================================
// Helper: Download file contents from GitHub
// ===========================================================================
Func<string, string> DownloadFile = (url) =>
{
    try
    {
        return client.GetStringAsync(url).Result;
    }
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show($"Error downloading file: {ex.Message}");
        return "";
    }
};


// ===========================================================================
// Helper: Recursively scan repo for .dax files
// Returns a list of objects with { Name, Url, Path }
// ===========================================================================
Func<string,string,System.Collections.Generic.List<dynamic>> GetFuncs = null;
GetFuncs = (apiUrl, relPath) =>
{
    var list = new System.Collections.Generic.List<dynamic>();
    try
    {
        var response = client.GetStringAsync(apiUrl).Result;
        var items = Newtonsoft.Json.Linq.JArray.Parse(response);

        foreach (var item in items)
        {
            var type = item["type"].ToString();
            var name = item["name"].ToString();

            if (type == "dir")
            {
                list.AddRange(GetFuncs(item["url"].ToString(),
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
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show($"Error scanning repo: {ex.Message}");
    }

    return list;
};


// ===========================================================================
// Helper: Upload updated file to GitHub
// ===========================================================================
Action<string,string,string> UploadToGitHub = (path, code, sha) =>
{
    try
    {
        var normalized = NormalizeLineEndings(code);
        string url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

        var body = new {
            message = "Update UDF from Tabular Editor",
            content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(normalized)),
            branch  = branch,
            sha     = sha
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var resp = client.PutAsync(url, new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")).Result;
        var respText = resp.Content.ReadAsStringAsync().Result;

        if (!resp.IsSuccessStatusCode)
            throw new Exception(resp.StatusCode + ": " + respText);
    }
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show($"Error uploading to GitHub: {ex.Message}");
    }
};


// ===========================================================================
// Load functions from GitHub
// ===========================================================================
var funcs = GetFuncs(repoApiUrl, "");
if (funcs.Count == 0)
{
    System.Windows.Forms.MessageBox.Show("No DAX functions found in the repository.");
    return;
}

var existing = new System.Collections.Generic.HashSet<string>(
    Model.Functions.Select(f => f.Name),
    StringComparer.OrdinalIgnoreCase
);


// ===========================================================================
// UI: Form and TreeView (main function list)
// ===========================================================================
var form = new System.Windows.Forms.Form();
form.Text = "GitHub DAX UDF Manager for TE3";
form.Width = 700; 
form.Height = 680;

var tree = new System.Windows.Forms.TreeView();
tree.CheckBoxes = true;
tree.Dock = System.Windows.Forms.DockStyle.Top;
tree.Height = 500;

var root = new System.Windows.Forms.TreeNode($"{owner}/{repo}/UDF");
tree.Nodes.Add(root);

// Helper: create/find nodes for folder structure
System.Windows.Forms.TreeNode GetOrCreateNode(System.Windows.Forms.TreeNodeCollection nodes, string name)
{
    foreach (System.Windows.Forms.TreeNode n in nodes)
        if (n.Text == name) return n;

    var newNode = new System.Windows.Forms.TreeNode(name);
    nodes.Add(newNode);
    return newNode;
}

// Build tree structure
foreach (var f in funcs.OrderBy(x => x.Path).ThenBy(x => x.Name))
{
    var pathParts = string.IsNullOrEmpty(f.Path)
        ? Array.Empty<string>()
        : f.Path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

    var current = root.Nodes;
    foreach (var part in pathParts)
        current = GetOrCreateNode(current, part).Nodes;

    var node = new System.Windows.Forms.TreeNode(f.Name) { Tag = f };
    if (existing.Contains(f.Name))
        node.NodeFont = new System.Drawing.Font(tree.Font, System.Drawing.FontStyle.Bold);

    current.Add(node);
}

tree.ExpandAll();
form.Controls.Add(tree);


// ===========================================================================
// UI: Buttons
// ===========================================================================
var btnWidth = 180;
var compare = new System.Windows.Forms.Button { Text="Compare", Width=btnWidth, Top=555, Left=50 };
var updateModel = new System.Windows.Forms.Button { Text="Update in the Model", Width=btnWidth, Top=555, Left=250, DialogResult=System.Windows.Forms.DialogResult.OK };
var updateGitHub = new System.Windows.Forms.Button { Text="Update in GitHub", Width=btnWidth, Top=555, Left=450 };
var openGitHub = new System.Windows.Forms.Button { Text="Open GitHub", Width=btnWidth, Top=595, Left=50 };
var cancel = new System.Windows.Forms.Button { Text="Cancel", Width=btnWidth, Top=595, Left=450, DialogResult=System.Windows.Forms.DialogResult.Cancel };


// ===========================================================================
// UI: Legend Panel (status explanation)
// ===========================================================================
var legend = new System.Windows.Forms.Panel();
legend.Top = 510;
legend.Left = 10;
legend.Width = 680;
legend.Height = 40;

void AddLegendItem(string text, System.Drawing.Color color, System.Drawing.FontStyle style, int left)
{
    var lbl = new System.Windows.Forms.Label();
    lbl.Text = text;
    lbl.AutoSize = true;
    lbl.Left = left;
    lbl.Top = 10;
    lbl.ForeColor = color;
    lbl.Font = new System.Drawing.Font(tree.Font, style);
    legend.Controls.Add(lbl);
}

AddLegendItem("Normal = not in the model", System.Drawing.Color.Black, System.Drawing.FontStyle.Regular, 5);
AddLegendItem("Bold = exists in model", System.Drawing.Color.Black, System.Drawing.FontStyle.Bold, 200);
AddLegendItem("Green = match", System.Drawing.Color.Green, System.Drawing.FontStyle.Bold, 400);
AddLegendItem("Red = differs", System.Drawing.Color.Red, System.Drawing.FontStyle.Bold, 530);

form.Controls.Add(legend);
form.Controls.Add(updateModel);
form.Controls.Add(updateGitHub);
form.Controls.Add(compare);
form.Controls.Add(openGitHub);
form.Controls.Add(cancel);


// ===========================================================================
// Button: Open GitHub in browser
// ===========================================================================
openGitHub.Click += (s, e) =>
{
    string url = $"https://github.com/{owner}/{repo}/tree/{branch}/UDF";
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show($"Error opening GitHub: {ex.Message}");
    }
};


// ===========================================================================
// Helper: Traverse Tree Nodes recursively
// ===========================================================================
void TraverseNodes(System.Windows.Forms.TreeNodeCollection nodes, Action<System.Windows.Forms.TreeNode> action)
{
    foreach (System.Windows.Forms.TreeNode node in nodes)
    {
        action(node);
        if (node.Nodes.Count > 0)
            TraverseNodes(node.Nodes, action);
    }
}


// ===========================================================================
// Compare logic: check if model vs GitHub functions match
// ===========================================================================
compare.Click += (s,e) =>
{
    var existingNow = new System.Collections.Generic.HashSet<string>(
        Model.Functions.Select(fn => fn.Name),
        StringComparer.OrdinalIgnoreCase
    );

    TraverseNodes(tree.Nodes, node =>
    {
        node.ForeColor = System.Drawing.Color.Black;
        node.ToolTipText = "";

        if (node.Tag == null) return;

        var f = (dynamic)node.Tag;
        if (existingNow.Contains((string)f.Name))
        {
            try
            {
                var path = string.IsNullOrEmpty(f.Path)
                    ? $"UDF/{f.Name}.dax"
                    : $"UDF/{f.Path.Replace("\\", "/")}/{f.Name}.dax";

                var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";
                var json = client.GetStringAsync(url).Result;
                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                var base64 = (string)obj["content"];
                var code = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));

                var modelFn = Model.Functions
                    .FirstOrDefault(fn => fn.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase));

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
            catch (Exception ex)
            {
                node.ForeColor = System.Drawing.Color.DarkOrange;
                node.ToolTipText = $"Error comparing: {ex.Message}";
            }
        }
    });
};


// ===========================================================================
// GitHub update logic: push model functions to GitHub
// ===========================================================================
updateGitHub.Click += (s,e) =>
{
    var selected = new System.Collections.Generic.List<dynamic>();
    TraverseNodes(tree.Nodes, node =>
    {
        if (node.Checked && node.Tag != null)
            selected.Add((dynamic)node.Tag);
    });

    foreach (var f in selected)
    {
        try
        {
            var path = string.IsNullOrEmpty(f.Path) 
                ? $"UDF/{f.Name}.dax" 
                : $"UDF/{f.Path.Replace("\\", "/")}/{f.Name}.dax";

            string sha = null;
            var resp = client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}").Result;
            if (resp.IsSuccessStatusCode)
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                sha = (string)obj["sha"];
            }

            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            if (fn != null)
            {
                var normalized = NormalizeLineEndings(fn.Expression);
                UploadToGitHub(path, normalized, sha);
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


// ===========================================================================
// Run dialog: apply user selections to model
// ===========================================================================
if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
{
    var selected = new System.Collections.Generic.List<dynamic>();
    TraverseNodes(tree.Nodes, node =>
    {
        if (node.Checked && node.Tag != null)
            selected.Add((dynamic)node.Tag);
    });

    foreach (var f in selected)
    {
        var dax = DownloadFile(f.Url);
        if (!string.IsNullOrWhiteSpace(dax))
        {
            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            var normalized = NormalizeLineEndings(dax);
            if (fn == null) Model.AddFunction(f.Name, normalized);
            else fn.Expression = normalized;
        }
    }
    System.Windows.Forms.MessageBox.Show($"Loaded {selected.Count} UDF(s) into the model.");
}
