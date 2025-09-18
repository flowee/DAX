// ===========================================================================
// TE3 Macro: GitHub DAX UDF Manager for TE3
// ---------------------------------------------------------------------------
// Name:
//   GitHub DAX UDF Manager for TE3
//
// Purpose:
//   Manage DAX user-defined functions (UDFs) between Tabular Editor 3 and a
//   GitHub repository. Load/compare/update/create UDF files with clear visual
//   indicators and safe, user-friendly workflows.
//
// Requirements:
//   - GitHub personal access token (classic or fine-grained) in environment
//     variable GITHUB_TOKEN (User or Machine scope).
//
// Links:
//   - Project repo: https://github.com/avatorl/DAX/tree/master/UDF/_TE3
//   - Blog: https://powerofbi.org/
//   - LinkedIn: https://www.linkedin.com/in/avatorl/
//
// ===========================================================================


// ===========================================================================
// Repository configuration
// ===========================================================================
var owner  = "avatorl";
var repo   = "DAX";
var folder = "UDF";
var branch = "master";


// ===========================================================================
// Small helpers (inline) to keep the script tidy
// ===========================================================================
Func<string, string> UrlEncode = s => System.Uri.EscapeDataString(s ?? "");

Func<string, string> Truncate = (text) =>
{
    if (string.IsNullOrEmpty(text)) return "";
    return text.Length <= 300 ? text : text.Substring(0, 300) + "...";
};

Action<System.Windows.Forms.Control, System.Action> WithWaitCursor = (ctl, action) =>
{
    var old = ctl.Cursor;
    try
    {
        ctl.Cursor = System.Windows.Forms.Cursors.WaitCursor;
        action();
    }
    finally
    {
        ctl.Cursor = old;
    }
};


// ===========================================================================
// GitHub URL builders (centralized templates)
// ===========================================================================
Func<string, string> GetApiUrl = (path) =>
    $"https://api.github.com/repos/{owner}/{repo}/contents/{UrlEncode(path)}?ref={UrlEncode(branch)}";

Func<string, string> GetUploadUrl = (path) =>
    $"https://api.github.com/repos/{owner}/{repo}/contents/{UrlEncode(path)}";

Func<string> GetBrowserUrl = () =>
    $"https://github.com/{owner}/{repo}/tree/{branch}/{folder}";

Func<dynamic, string> GetFilePath = (f) =>
    string.IsNullOrEmpty(f.Path)
        ? $"{folder}/{f.Name}.dax"
        : $"{folder}/{f.Path.Replace('\\','/').Trim('/')}/{f.Name}.dax";


// ===========================================================================
// GitHub Token (required)
// ===========================================================================
var githubToken = System.Environment.GetEnvironmentVariable("GITHUB_TOKEN", System.EnvironmentVariableTarget.User)
                ?? System.Environment.GetEnvironmentVariable("GITHUB_TOKEN", System.EnvironmentVariableTarget.Machine);

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
client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");


// ===========================================================================
// Helper: Normalize line endings
// Ensures consistent formatting of UDF code before comparing/uploading
// ===========================================================================
Func<string, string> NormalizeLineEndings = text =>
{
    if (string.IsNullOrEmpty(text)) return "";

    // Normalize CRLF/CR to LF
    var norm = text.Replace("\r\n", "\n").Replace("\r", "\n");

    // Trim trailing spaces on each line (keeps leading newlines intact)
    norm = string.Join("\n", norm.Split('\n').Select(line => line.TrimEnd()));

    // Ensure trailing newline for consistency
    if (!norm.EndsWith("\n")) norm += "\n";
    return norm;
};


// ===========================================================================
// Helper: Recursively scan repo for .dax files
// Returns a list of objects with { Name, Url, Path }
// ===========================================================================
System.Collections.Generic.List<dynamic> GetFuncs(string apiUrl, string relPath)
{
    var list = new System.Collections.Generic.List<dynamic>();
    try
    {
        var responseText = client.GetStringAsync(apiUrl).Result;
        var items = Newtonsoft.Json.Linq.JArray.Parse(responseText);

        foreach (var item in items)
        {
            var type = item["type"]?.ToString();
            var name = item["name"]?.ToString() ?? "";

            if (type == "dir")
            {
                var nextApi = item["url"]?.ToString();
                var nextRel = string.IsNullOrEmpty(relPath) ? name : $"{relPath}/{name}";
                list.AddRange(GetFuncs(nextApi, nextRel));
            }
            else if (type == "file" && name.EndsWith(".dax", System.StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new {
                    Name = System.IO.Path.GetFileNameWithoutExtension(name),
                    Url  = item["download_url"]?.ToString(),
                    Path = relPath // keep with forward slashes
                });
            }
        }
    }
    catch (System.Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("GetFuncs error: " + ex);
        System.Windows.Forms.MessageBox.Show($"Error scanning repo: {ex.Message}");
    }

    return list;
}


// ===========================================================================
// Helper: Upload updated/new file to GitHub
// ===========================================================================
Action<string,string,string> UploadToGitHub = (path, code, sha) =>
{
    try
    {
        var normalized = NormalizeLineEndings(code);
        var url = GetUploadUrl(path);

        var body = new {
            message = $"Update UDF '{System.IO.Path.GetFileNameWithoutExtension(path)}' via GitHub DAX UDF Manager for TE3",
            content = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(normalized)),
            branch  = branch,
            sha     = sha
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var resp = client.PutAsync(url, new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")).Result;
        var respText = resp.Content.ReadAsStringAsync().Result;

        if (!resp.IsSuccessStatusCode)
            throw new System.Exception($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(respText)}");
    }
    catch (System.Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("UploadToGitHub error: " + ex);
        System.Windows.Forms.MessageBox.Show($"Error uploading to GitHub: {ex.Message}");
    }
};


// ===========================================================================
// UI: Form, Layout, TreeView
// ===========================================================================
var form = new System.Windows.Forms.Form();
form.Text = "GitHub DAX UDF Manager for TE3";
form.Width = 720;
form.Height = 720;
form.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
form.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

// TableLayout: Row0 Tree (100%), Row1 Legend (Auto), Row2 Buttons (Auto)
var layout = new System.Windows.Forms.TableLayoutPanel();
layout.Dock = System.Windows.Forms.DockStyle.Fill;
layout.ColumnCount = 1;
layout.RowCount = 3;
layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
form.Controls.Add(layout);

var tree = new System.Windows.Forms.TreeView();
tree.CheckBoxes = true;
tree.Dock = System.Windows.Forms.DockStyle.Fill;
tree.HideSelection = false;
tree.FullRowSelect = true;
tree.ShowNodeToolTips = true;
layout.Controls.Add(tree, 0, 0);


// ===========================================================================
// UI: Legend Panel (status explanation)
// ===========================================================================
var legend = new System.Windows.Forms.FlowLayoutPanel();
legend.AutoSize = true;
legend.Dock = System.Windows.Forms.DockStyle.Fill;
legend.WrapContents = false;
legend.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);

void AddLegendItem(string text, System.Drawing.Color color, System.Drawing.FontStyle style)
{
    var lbl = new System.Windows.Forms.Label();
    lbl.Text = text;
    lbl.AutoSize = true;
    lbl.Margin = new System.Windows.Forms.Padding(8, 4, 8, 4);
    lbl.ForeColor = color;
    lbl.Font = new System.Drawing.Font(tree.Font, style);
    legend.Controls.Add(lbl);
}

AddLegendItem("Normal = not in model", System.Drawing.Color.Black, System.Drawing.FontStyle.Regular);
AddLegendItem("Bold = exists in model", System.Drawing.Color.Black, System.Drawing.FontStyle.Bold);
AddLegendItem("Green = match", System.Drawing.Color.Green, System.Drawing.FontStyle.Bold);
AddLegendItem("Red = differs", System.Drawing.Color.Red, System.Drawing.FontStyle.Bold);
AddLegendItem("Blue = model-only UDF", System.Drawing.Color.Blue, System.Drawing.FontStyle.Bold);

layout.Controls.Add(legend, 0, 1);


// ===========================================================================
// UI: Buttons (FlowLayout right-aligned)
// ===========================================================================
var buttons = new System.Windows.Forms.FlowLayoutPanel();
buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
buttons.Dock = System.Windows.Forms.DockStyle.Fill;
buttons.AutoSize = true;

// Panel: only top/bottom padding, no left gap
buttons.Padding = new System.Windows.Forms.Padding(0, 8, 8, 8);

var compare     = new System.Windows.Forms.Button { Text="Compare", AutoSize = true };
var updateModel = new System.Windows.Forms.Button { Text="Update in the Model", AutoSize = true };
var updateGitHub= new System.Windows.Forms.Button { Text="Update in GitHub", AutoSize = true };
var createGitHub= new System.Windows.Forms.Button { Text="Create in GitHub", AutoSize = true };
var openGitHub  = new System.Windows.Forms.Button { Text="Open GitHub", AutoSize = true };
var cancel      = new System.Windows.Forms.Button { Text="Close", AutoSize = true, DialogResult = System.Windows.Forms.DialogResult.Cancel };

// Add buttons in reverse order (because of RightToLeft flow)
buttons.Controls.Add(cancel);
buttons.Controls.Add(openGitHub);
buttons.Controls.Add(createGitHub);
buttons.Controls.Add(updateGitHub);
buttons.Controls.Add(updateModel);
buttons.Controls.Add(compare);

// Ensure buttons don't add their own extra left margin (apply *after* adding)
foreach (System.Windows.Forms.Control ctrl in buttons.Controls)
{
    ctrl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
}

layout.Controls.Add(buttons, 0, 2);

form.CancelButton = cancel; // Esc closes


// ===========================================================================
// Helper: Build/refresh tree (repo + model-only UDFs)
// ===========================================================================
Action RefreshTree = () =>
{
    WithWaitCursor(form, () =>
    {
        var funcs = GetFuncs(GetApiUrl(folder), "");
        var existing = new System.Collections.Generic.HashSet<string>(
            Model.Functions.Select(f => f.Name),
            System.StringComparer.OrdinalIgnoreCase
        );

        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            var root = new System.Windows.Forms.TreeNode($"{owner}/{repo}/{folder}");
            tree.Nodes.Add(root);

            // Helper: create/find child node by text
            System.Windows.Forms.TreeNode GetOrCreateNode(System.Windows.Forms.TreeNodeCollection nodes, string name)
            {
                foreach (System.Windows.Forms.TreeNode n in nodes)
                    if (n.Text == name) return n;
                var newNode = new System.Windows.Forms.TreeNode(name);
                nodes.Add(newNode);
                return newNode;
            }

            // Build repo branch
            foreach (var f in funcs.OrderBy(x => x.Path).ThenBy(x => x.Name))
            {
                var pathParts = string.IsNullOrEmpty(f.Path)
                    ? System.Array.Empty<string>()
                    : f.Path.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);

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
                if (!funcs.Any(f => string.Equals(f.Name, fn.Name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    var node = new System.Windows.Forms.TreeNode(fn.Name) { Tag = fn };
                    node.NodeFont = new System.Drawing.Font(tree.Font, System.Drawing.FontStyle.Bold);
                    node.ForeColor = System.Drawing.Color.Blue;
                    modelOnlyRoot.Nodes.Add(node);
                }
            }

            tree.ExpandAll();
        }
        finally
        {
            tree.EndUpdate();
        }
    });
};

// Initial load
RefreshTree();


// ===========================================================================
// Helper: Traverse tree recursively
// ===========================================================================
void TraverseNodes(System.Windows.Forms.TreeNodeCollection nodes, System.Action<System.Windows.Forms.TreeNode> action)
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
    catch (System.Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Open GitHub error: " + ex);
        System.Windows.Forms.MessageBox.Show($"Error opening GitHub: {ex.Message}");
    }
};


// ===========================================================================
// Button: Create GitHub files for model-only UDFs (with existence check)
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

    WithWaitCursor(form, () =>
    {
        int createdOrUpdated = 0;

        foreach (var fn in selected)
        {
            try
            {
                var path = $"{folder}/{fn.Name}.dax";
                string sha = null;

                // Check if file exists
                var checkResp = client.GetAsync(GetApiUrl(path)).Result;
                if (checkResp.IsSuccessStatusCode)
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(checkResp.Content.ReadAsStringAsync().Result);
                    sha = (string)obj["sha"];

                    // Ask for overwrite
                    var ans = System.Windows.Forms.MessageBox.Show(
                        $"'{fn.Name}.dax' already exists in GitHub.\nDo you want to overwrite it?",
                        "File exists",
                        System.Windows.Forms.MessageBoxButtons.YesNoCancel,
                        System.Windows.Forms.MessageBoxIcon.Question);

                    if (ans == System.Windows.Forms.DialogResult.Cancel) return; // abort whole operation
                    if (ans == System.Windows.Forms.DialogResult.No) continue;   // skip this one
                    // Yes → proceed with existing sha
                }

                var normalized = NormalizeLineEndings(fn.Expression);
                UploadToGitHub(path, normalized, sha);
                createdOrUpdated++;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CreateGitHub error: " + ex);
                System.Windows.Forms.MessageBox.Show($"Error creating {fn.Name}: {ex.Message}");
            }
        }

        RefreshTree();
        System.Windows.Forms.MessageBox.Show($"Created/updated {createdOrUpdated} UDF(s) in GitHub.");
    });
};


// ===========================================================================
// Button: Compare model vs GitHub (with small content cache)
// ===========================================================================
compare.Click += (s, e) =>
{
    WithWaitCursor(form, () =>
    {
        var existingNow = new System.Collections.Generic.HashSet<string>(
            Model.Functions.Select(fn => fn.Name),
            System.StringComparer.OrdinalIgnoreCase
        );

        // ephemeral cache for this compare run
        var contentCache = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        TraverseNodes(tree.Nodes, node =>
        {
            node.ForeColor = System.Drawing.Color.Black;
            node.ToolTipText = "";

            if (node.Tag == null) return;

            var f = (dynamic)node.Tag;

            // Only compare repo items that also exist in the model
            if (existingNow.Contains((string)f.Name))
            {
                try
                {
                    var apiPath = GetFilePath(f);
                    string code;

                    // Use cache to avoid repeated API calls
                    if (!contentCache.TryGetValue(apiPath, out code))
                    {
                        var json = client.GetStringAsync(GetApiUrl(apiPath)).Result;
                        var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                        var base64 = (string)obj["content"];
                        code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(base64));
                        contentCache[apiPath] = code;
                    }

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

    if (selected.Count == 0)
    {
        System.Windows.Forms.MessageBox.Show("No UDFs selected.");
        return;
    }

    WithWaitCursor(form, () =>
    {
        int updated = 0;

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

                var fn = Model.Functions.FirstOrDefault(x => x.Name.Equals(f.Name, System.StringComparison.OrdinalIgnoreCase));
                if (fn != null)
                {
                    var normalized = NormalizeLineEndings(fn.Expression);

                    // Update description from first comment line (if exists)
                    var lines = normalized.Split(new[] { '\n' }, System.StringSplitOptions.None);
                    if (lines.Length > 0 && lines[0].TrimStart().StartsWith("//"))
                        fn.Description = lines[0].Trim().Substring(2).Trim();

                    UploadToGitHub(path, normalized, sha);
                    // Keep model expression normalized for future comparisons
                    fn.Expression = normalized;

                    updated++;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateGitHub error: " + ex);
                System.Windows.Forms.MessageBox.Show($"Error updating {f.Name}: {ex.Message}");
            }
        }

        RefreshTree();
        System.Windows.Forms.MessageBox.Show($"Updated {updated} UDF(s) in GitHub.");
    });
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

    if (selected.Count == 0)
    {
        System.Windows.Forms.MessageBox.Show("No UDFs selected.");
        return;
    }

    WithWaitCursor(form, () =>
    {
        int updated = 0;

        foreach (var f in selected)
        {
            try
            {
                var path = GetFilePath(f);
                var json = client.GetStringAsync(GetApiUrl(path)).Result;
                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                var base64 = (string)obj["content"];
                var code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(base64));
                var normalized = NormalizeLineEndings(code);

                // Extract description from first comment line
                string desc = null;
                var lines = normalized.Split(new[] { '\n' }, System.StringSplitOptions.None);
                if (lines.Length > 0 && lines[0].TrimStart().StartsWith("//"))
                    desc = lines[0].Trim().Substring(2).Trim();

                var fn = Model.Functions.FirstOrDefault(x => x.Name.Equals(f.Name, System.StringComparison.OrdinalIgnoreCase));
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

                updated++;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateModel error: " + ex);
                System.Windows.Forms.MessageBox.Show($"Error updating model with {f.Name}: {ex.Message}");
            }
        }

        System.Windows.Forms.MessageBox.Show($"Updated {updated} UDF(s) in the Model.");
    });
};


// ===========================================================================
// Run the form
// ===========================================================================
form.ShowDialog();
