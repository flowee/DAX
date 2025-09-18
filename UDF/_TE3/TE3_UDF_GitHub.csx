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
//       * Create new GitHub files for model-only UDFs.
//       * Visual status indicators in a TreeView (normal, bold, green, red, blue).
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
var owner  = "avatorl";
var repo   = "DAX";
var folder = "UDF";
var branch = "master";


// ===========================================================================
// GitHub URL builders (centralized templates)
// ===========================================================================
Func<string, string> GetApiUrl = (path) =>
    $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";

Func<string, string> GetUploadUrl = (path) =>
    $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

Func<string> GetBrowserUrl = () =>
    $"https://github.com/{owner}/{repo}/tree/{branch}/{folder}";

Func<dynamic, string> GetFilePath = (f) =>
    string.IsNullOrEmpty(f.Path)
        ? $"{folder}/{f.Name}.dax"
        : $"{folder}/{f.Path.Replace("\\", "/")}/{f.Name}.dax";


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
// Helper: Upload updated/new file to GitHub
// ===========================================================================
Action<string,string,string> UploadToGitHub = (path, code, sha) =>
{
    try
    {
        var normalized = NormalizeLineEndings(code);
        string url = GetUploadUrl(path);

        var body = new {
            message = $"Update UDF '{System.IO.Path.GetFileNameWithoutExtension(path)}' via GitHub DAX UDF Manager for TE3",
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
form.Controls.Add(tree);


// ===========================================================================
// Helper: Build/refresh tree (repo + model-only UDFs)
// ===========================================================================
Action RefreshTree = () =>
{
    var funcs = GetFuncs(GetApiUrl(folder), "");
    var existing = new System.Collections.Generic.HashSet<string>(
        Model.Functions.Select(f => f.Name),
        StringComparer.OrdinalIgnoreCase
    );

    tree.Nodes.Clear();
    var root = new System.Windows.Forms.TreeNode($"{owner}/{repo}/{folder}");
    tree.Nodes.Add(root);

    // Build repo branch
    System.Windows.Forms.TreeNode GetOrCreateNode(System.Windows.Forms.TreeNodeCollection nodes, string name)
    {
        foreach (System.Windows.Forms.TreeNode n in nodes)
            if (n.Text == name) return n;
        var newNode = new System.Windows.Forms.TreeNode(name);
        nodes.Add(newNode);
        return newNode;
    }

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

    // Build model-only branch
    var modelOnlyRoot = new System.Windows.Forms.TreeNode("Model-only UDFs");
    tree.Nodes.Add(modelOnlyRoot);

    foreach (var fn in Model.Functions)
    {
        if (!funcs.Any(f => f.Name.Equals(fn.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var node = new System.Windows.Forms.TreeNode(fn.Name) { Tag = fn };
            node.NodeFont = new System.Drawing.Font(tree.Font, System.Drawing.FontStyle.Bold);
            node.ForeColor = System.Drawing.Color.Blue;
            modelOnlyRoot.Nodes.Add(node);
        }
    }

    tree.ExpandAll();
};

// Initial load
RefreshTree();


// ===========================================================================
// UI: Buttons
// ===========================================================================
var btnWidth = 180;
var compare     = new System.Windows.Forms.Button { Text="Compare", Width=btnWidth, Top=555, Left=50 };
var updateModel = new System.Windows.Forms.Button { Text="Update in the Model", Width=btnWidth, Top=555, Left=250 };
var updateGitHub= new System.Windows.Forms.Button { Text="Update in GitHub", Width=btnWidth, Top=555, Left=450 };
var openGitHub  = new System.Windows.Forms.Button { Text="Open GitHub", Width=btnWidth, Top=595, Left=50 };
var createGitHub= new System.Windows.Forms.Button { Text="Create in GitHub", Width=btnWidth, Top=595, Left=250 };
var cancel      = new System.Windows.Forms.Button { Text="Cancel", Width=btnWidth, Top=595, Left=450, DialogResult=System.Windows.Forms.DialogResult.Cancel };

form.Controls.AddRange(new System.Windows.Forms.Control[] { compare, updateModel, updateGitHub, openGitHub, cancel, createGitHub });


// ===========================================================================
// UI: Legend Panel (status explanation)
// ===========================================================================
var legend = new System.Windows.Forms.Panel();
legend.Top = 510;
legend.Left = 10;
legend.Width = 680;
legend.Height = 40;

int currentLeft = 5;
void AddLegendItem(string text, System.Drawing.Color color, System.Drawing.FontStyle style)
{
    var lbl = new System.Windows.Forms.Label();
    lbl.Text = text;
    lbl.AutoSize = true;
    lbl.Left = currentLeft;
    lbl.Top = 10;
    lbl.ForeColor = color;
    lbl.Font = new System.Drawing.Font(tree.Font, style);
    legend.Controls.Add(lbl);

    // Move next label to the right of this one + 20px padding
    currentLeft += lbl.Width + 20;
}

AddLegendItem("Normal = not in model", System.Drawing.Color.Black, System.Drawing.FontStyle.Regular);
AddLegendItem("Bold = exists in model", System.Drawing.Color.Black, System.Drawing.FontStyle.Bold);
AddLegendItem("Green = match", System.Drawing.Color.Green, System.Drawing.FontStyle.Bold);
AddLegendItem("Red = differs", System.Drawing.Color.Red, System.Drawing.FontStyle.Bold);
AddLegendItem("Blue = model-only UDF", System.Drawing.Color.Blue, System.Drawing.FontStyle.Bold);

form.Controls.Add(legend);



// ===========================================================================
// Helper: Traverse tree recursively
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
// Button: Open GitHub in browser
// ===========================================================================
openGitHub.Click += (s, e) =>
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = GetBrowserUrl(),
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show($"Error opening GitHub: {ex.Message}");
    }
};


// ===========================================================================
// Button: Create GitHub files for model-only UDFs
// ===========================================================================
createGitHub.Click += (s, e) =>
{
    var selected = new System.Collections.Generic.List<TabularEditor.TOMWrapper.Function>();
    TraverseNodes(tree.Nodes, node =>
    {
        if (node.Checked && node.Tag is TabularEditor.TOMWrapper.Function fn)
            selected.Add(fn);
    });

    if (selected.Count == 0)
    {
        System.Windows.Forms.MessageBox.Show("No model-only UDFs selected.");
        return;
    }

    var folderPath = folder; // always save under UDF
    foreach (var fn in selected)
    {
        try
        {
            var path = $"{folderPath}/{fn.Name}.dax";
            var normalized = NormalizeLineEndings(fn.Expression);
            UploadToGitHub(path, normalized, null);
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error creating {fn.Name}: {ex.Message}");
        }
    }

    RefreshTree();
    System.Windows.Forms.MessageBox.Show($"Created {selected.Count} UDF(s) in GitHub.");
};


// ===========================================================================
// Button: Compare model vs GitHub
// ===========================================================================
compare.Click += (s, e) =>
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
                var json = client.GetStringAsync(GetApiUrl(GetFilePath(f))).Result;
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
// Button: Update GitHub with selected model functions
// ===========================================================================
updateGitHub.Click += (s, e) =>
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
            var path = GetFilePath(f);
            string sha = null;

            var resp = client.GetAsync(GetApiUrl(path)).Result;
            if (resp.IsSuccessStatusCode)
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                sha = (string)obj["sha"];
            }

            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            if (fn != null)
            {
                var normalized = NormalizeLineEndings(fn.Expression);

                // Update description from first comment line (if exists)
                var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
                if (lines.Length > 0 && lines[0].TrimStart().StartsWith("//"))
                    fn.Description = lines[0].Trim().Substring(2).Trim();

                UploadToGitHub(path, normalized, sha);
                fn.Expression = normalized;
            }
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error updating {f.Name}: {ex.Message}");
        }
    }

    RefreshTree();
    System.Windows.Forms.MessageBox.Show($"Updated {selected.Count} UDF(s) in GitHub.");
};

// ===========================================================================
// Update Model logic: pull UDFs from GitHub into the model
// ===========================================================================
updateModel.Click += (s, e) =>
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
            var path = GetFilePath(f);
            var json = client.GetStringAsync(GetApiUrl(path)).Result;
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var base64 = (string)obj["content"];
            var code = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var normalized = NormalizeLineEndings(code);

            // Extract description from first comment line
            string desc = null;
            var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length > 0 && lines[0].TrimStart().StartsWith("//"))
                desc = lines[0].Trim().Substring(2).Trim();

            var fn = Model.Functions.FirstOrDefault(x => x.Name == f.Name);
            if (fn == null)
            {
                fn = Model.AddFunction(f.Name, normalized);
                if (!string.IsNullOrEmpty(desc)) fn.Description = desc;
            }
            else
            {
                fn.Expression = normalized;
                if (!string.IsNullOrEmpty(desc)) fn.Description = desc;
            }
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error updating model with {f.Name}: {ex.Message}");
        }
    }

    System.Windows.Forms.MessageBox.Show($"Updated {selected.Count} UDF(s) in the Model.");
};


// ===========================================================================
// Run the form
// ===========================================================================
form.ShowDialog();
