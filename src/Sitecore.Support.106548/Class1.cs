namespace Sitecore.Support.Shell.Controls.RichTextEditor.InsertImage
{
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.IO;
    using Sitecore.Resources;
    using Sitecore.Resources.Media;
    using Sitecore.Shell;
    using Sitecore.Shell.Framework;
    using Sitecore.StringExtensions;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Pages;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls;
    using System;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Web;
    using System.Web.UI;

    public class InsertImageForm : DialogForm
    {
        protected Sitecore.Web.UI.HtmlControls.DataContext DataContext;
        protected Button EditButton;
        protected Sitecore.Web.UI.HtmlControls.Edit Filename;
        protected Sitecore.Web.UI.HtmlControls.Edit FileId;
        private const string InsertMediaFolderRegistryKey = "/Current_User/InsertMedia/Folder";
        protected Scrollbox Listview;
        protected TreeviewEx Treeview;
        protected Button Upload;

        protected void Edit()
        {
            Item selectionItem = this.Treeview.GetSelectionItem();
            if (((selectionItem == null) || (selectionItem.TemplateID == TemplateIDs.MediaFolder)) || (selectionItem.TemplateID == TemplateIDs.MainSection))
            {
                SheerResponse.Alert("Select a media item.", new string[0]);
            }
            else
            {
                UrlString str = new UrlString("/sitecore/shell/Applications/Content Manager/default.aspx");
                str["fo"] = selectionItem.ID.ToString();
                str["mo"] = "popup";
                str["wb"] = "0";
                str["pager"] = "0";
                str[Sitecore.Configuration.State.Client.UsesBrowserWindowsQueryParameterName] = "1";
                Context.ClientPage.ClientResponse.ShowModalDialog(str.ToString(), string.Equals(Context.Language.Name, "ja-jp", StringComparison.InvariantCultureIgnoreCase) ? "1115" : "955", "560");
            }
        }

        private Item GetCurrentItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string str = message["id"];
            Language language = this.DataContext.Language;
            Item folder = this.DataContext.GetFolder();
            if (folder != null)
            {
                language = folder.Language;
            }
            if (!string.IsNullOrEmpty(str))
            {
                return Sitecore.Client.ContentDatabase.Items[str, language];
            }
            return folder;
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            if (message.Name == "item:load")
            {
                this.LoadItem(message);
            }
            else
            {
                Dispatcher.Dispatch(message, this.GetCurrentItem(message));
                base.HandleMessage(message);
            }
        }

        protected void Listview_Click(string id)
        {
            Assert.ArgumentNotNullOrEmpty(id, "id");
            Item item = Sitecore.Client.ContentDatabase.GetItem(id, this.ContentLanguage);
            if (item != null)
            {
                this.SelectItem(item, true);
            }
        }

        private void LoadItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            Language language = this.DataContext.Language;
            Item folder = this.DataContext.GetFolder();
            if (folder != null)
            {
                language = folder.Language;
            }
            Item item = Sitecore.Client.ContentDatabase.GetItem(ID.Parse(message["id"]), language);
            if (item != null)
            {
                this.SelectItem(item, true);
            }
        }

        protected override void OnCancel(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this.Mode == "webedit")
            {
                base.OnCancel(sender, args);
            }
            else
            {
                SheerResponse.Eval("scCancel()");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                this.Mode = WebUtil.GetQueryString("mo");
                this.DataContext.GetFromQueryString();
                string queryString = WebUtil.GetQueryString("fo");
                if (ShortID.IsShortID(queryString))
                {
                    queryString = ShortID.Parse(queryString).ToID().ToString();
                    this.DataContext.Folder = queryString;
                }
                Context.ClientPage.ServerProperties["mode"] = WebUtil.GetQueryString("mo");
                if (!string.IsNullOrEmpty(WebUtil.GetQueryString("databasename")))
                {
                    this.DataContext.Parameters = "databasename=" + WebUtil.GetQueryString("databasename");
                }
                if (string.IsNullOrEmpty(this.DataContext.Folder))
                {
                    this.DataContext.Folder = Registry.GetString("/Current_User/InsertMedia/Folder", string.Empty);
                }
                Item folder = this.DataContext.GetFolder();
                Assert.IsNotNull(folder, "Folder not found");
                this.SelectItem(folder, true);
                this.Upload.Click = "media:upload(edit=" + (Settings.Media.OpenContentEditorAfterUpload ? "1" : "0") + ",load=1)";
                this.Upload.ToolTip = Translate.Text("Upload a new media file to the Media Library");
                this.EditButton.ToolTip = Translate.Text("Edit the media item in the Content Editor.");
            }
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            //string str = this.Filename.Value;
            ID SelectedFileId = ID.Parse(this.FileId.Value);
            if (SelectedFileId.IsNull)
            {
                SheerResponse.Alert("Select a media item.", new string[0]);
            }
            else
            {
                //Item root = this.DataContext.GetRoot();
                //if (root != null)
                //{
                //    Item rootItem = root.Database.GetRootItem();
                //    if ((rootItem != null) && (root.ID != rootItem.ID))
                //    {
                //        str = FileUtil.MakePath(root.Paths.Path, str, '/');
                //    }
                //}
                MediaItem item = this.DataContext.GetItem(SelectedFileId);
                if (item == null)
                {
                    SheerResponse.Alert("The media item could not be found.", new string[0]);
                }
                else if (!(MediaManager.GetMedia(MediaUri.Parse((Item)item)) is ImageMedia))
                {
                    SheerResponse.Alert("The selected item is not an image. Select an image to continue.", new string[0]);
                }
                else
                {
                    MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
                    shellOptions.Language = this.ContentLanguage;
                    string text = !string.IsNullOrEmpty(HttpContext.Current.Request.Form["AlternateText"]) ? HttpContext.Current.Request.Form["AlternateText"] : item.Alt;
                    Tag image = new Tag("img");
                    this.SetDimensions(item, shellOptions, image);
                    image.Add("Src", MediaManager.GetMediaUrl(item, shellOptions));
                    image.Add("Alt", StringUtil.EscapeQuote(text));
                    image.Add("_languageInserted", "true");
                    if (this.Mode == "webedit")
                    {
                        SheerResponse.SetDialogValue(StringUtil.EscapeJavascriptString(image.ToString()));
                        base.OnOK(sender, args);
                    }
                    else
                    {
                        SheerResponse.Eval("scClose(" + StringUtil.EscapeJavascriptString(image.ToString()) + ")");
                    }
                    this.SaveFolderToRegistry();
                }
            }
        }

        private static void RenderEmpty(HtmlTextWriter output)
        {
            Assert.ArgumentNotNull(output, "output");
            output.Write("<table width=\"100%\" border=\"0\"><tr><td align=\"center\">");
            output.Write("<div style=\"padding:8px\">");
            output.Write(Translate.Text("This folder is empty."));
            output.Write("</div>");
            output.Write("</td></tr></table>");
        }

        private static void RenderListviewItem(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            MediaItem item2 = item;
            output.Write("<a href=\"#\" class=\"scTile\" onclick=\"javascript:return scForm.postEvent(this,event,'Listview_Click(&quot;" + item.ID + "&quot;)')\" >");
            output.Write("<div class=\"scTileImage\">");
            if (((item.TemplateID == TemplateIDs.Folder) || (item.TemplateID == TemplateIDs.TemplateFolder)) || (item.TemplateID == TemplateIDs.MediaFolder))
            {
                ImageBuilder builder2 = new ImageBuilder
                {
                    Src = item.Appearance.Icon,
                    Width = 0x30,
                    Height = 0x30,
                    Margin = "24px 24px 24px 24px"
                };
                builder2.Render(output);
            }
            else
            {
                MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
                shellOptions.AllowStretch = false;
                shellOptions.BackgroundColor = Color.White;
                shellOptions.Language = item.Language;
                shellOptions.Thumbnail = true;
                shellOptions.UseDefaultIcon = true;
                shellOptions.Width = 0x60;
                shellOptions.Height = 0x60;
                output.Write("<img src=\"" + MediaManager.GetMediaUrl(item2, shellOptions) + "\" class=\"scTileImageImage\" border=\"0\" alt=\"\" />");
            }
            output.Write("</div>");
            output.Write("<div class=\"scTileHeader\">");
            output.Write(item.DisplayName);
            output.Write("</div>");
            output.Write("</a>");
        }

        private static void RenderPreview(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            MediaItem item2 = item;
            MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
            shellOptions.AllowStretch = false;
            shellOptions.BackgroundColor = Color.White;
            shellOptions.Language = item.Language;
            shellOptions.Thumbnail = true;
            shellOptions.UseDefaultIcon = true;
            shellOptions.Width = 0xc0;
            shellOptions.Height = 0xc0;
            string mediaUrl = MediaManager.GetMediaUrl(item2, shellOptions);
            output.Write("<table width=\"100%\" height=\"100%\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\">");
            output.Write("<tr><td align=\"center\" height=\"100%\">");
            output.Write("<div class=\"scPreview\">");
            output.Write("<img src=\"" + mediaUrl + "\" class=\"scPreviewImage\" border=\"0\" alt=\"\" />");
            output.Write("</div>");
            output.Write("<div class=\"scPreviewHeader\">");
            output.Write(item.DisplayName);
            output.Write("</div>");
            output.Write("</td></tr>");
            if (!(MediaManager.GetMedia(MediaUri.Parse((Item)item2)) is ImageMedia))
            {
                output.Write("</table>");
            }
            else
            {
                output.Write("<tr><td class=\"scProperties\">");
                output.Write("<table border=\"0\" class=\"scFormTable\" cellpadding=\"2\" cellspacing=\"0\">");
                output.Write("<col align=\"right\" />");
                output.Write("<col align=\"left\" />");
                output.Write("<tr><td>");
                output.Write(Translate.Text("Alternate text:"));
                output.Write("</td><td>");
                output.Write("<input type=\"text\" id=\"AlternateText\" value=\"{0}\" />", WebUtil.HtmlEncode(item2.Alt));
                output.Write("</td></tr>");
                output.Write("<tr><td>");
                output.Write(Translate.Text("Width:"));
                output.Write("</td><td>");
                output.Write("<input type=\"text\" id=\"Width\" value=\"{0}\" />", WebUtil.HtmlEncode(item2.InnerItem["Width"]));
                output.Write("</td></tr>");
                output.Write("<tr><td>");
                output.Write(Translate.Text("Height:"));
                output.Write("</td><td>");
                output.Write("<input type=\"text\" id=\"Height\" value=\"{0}\" />", WebUtil.HtmlEncode(item2.InnerItem["Height"]));
                output.Write("</td></tr>");
                output.Write("</table>");
                output.Write("</td></tr>");
                output.Write("</table>");
                SheerResponse.Eval("scAspectPreserver.reload();");
            }
        }

        protected virtual void SaveFolderToRegistry()
        {
            Item item;
            Item item2;
            this.DataContext.GetState(out item, out item2);
            string str = (item.ID == item2.ID) ? item.ID.ToString() : item2.ParentID.ToString();
            Registry.SetString("/Current_User/InsertMedia/Folder", str);
        }

        private void SelectItem(Item item, bool expand = true)
        {
            Assert.ArgumentNotNull(item, "item");
            this.UploadButtonDisabled = !item.Access.CanCreate();
            this.Filename.Value = this.ShortenPath(item.Paths.Path);
            this.FileId.Value = item.ID.ToString();
            this.DataContext.SetFolder(item.Uri);
            if (expand)
            {
                this.Treeview.SetSelectedItem(item);
            }
            HtmlTextWriter output = new HtmlTextWriter(new StringWriter());
            if (((item.TemplateID == TemplateIDs.Folder) || (item.TemplateID == TemplateIDs.MediaFolder)) || (item.TemplateID == TemplateIDs.MainSection))
            {
                foreach (Item item2 in item.Children)
                {
                    if (item2.Appearance.Hidden)
                    {
                        if (Context.User.IsAdministrator && UserOptions.View.ShowHiddenItems)
                        {
                            RenderListviewItem(output, item2);
                        }
                    }
                    else
                    {
                        RenderListviewItem(output, item2);
                    }
                }
            }
            else
            {
                RenderPreview(output, item);
            }
            string str = output.InnerWriter.ToString();
            if (string.IsNullOrEmpty(str))
            {
                RenderEmpty(output);
                str = output.InnerWriter.ToString();
            }
            this.Listview.InnerHtml = str;
        }

        protected void SelectTreeNode()
        {
            Item selectionItem = this.Treeview.GetSelectionItem(this.ContentLanguage, Sitecore.Data.Version.Latest);
            if (selectionItem != null)
            {
                this.SelectItem(selectionItem, false);
            }
        }

        private void SetDimensions(MediaItem item, MediaUrlOptions options, Tag image)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(options, "options");
            Assert.ArgumentNotNull(image, "image");
            NameValueCollection form = HttpContext.Current.Request.Form;
            if ((!string.IsNullOrEmpty(form["Width"]) && (form["Width"] != item.InnerItem["Width"])) && (form["Height"] != item.InnerItem["Height"]))
            {
                int num;
                int num2;
                if (int.TryParse(form["Width"], out num))
                {
                    options.Width = num;
                    image.Add("width", num.ToString());
                }
                if (int.TryParse(form["Height"], out num2))
                {
                    options.Height = num2;
                    image.Add("height", num2.ToString());
                }
            }
            else
            {
                image.Add("width", item.InnerItem["Width"]);
                image.Add("height", item.InnerItem["Height"]);
            }
        }

        private string ShortenPath(string path)
        {
            Assert.ArgumentNotNull(path, "path");
            Item root = this.DataContext.GetRoot();
            if (root != null)
            {
                Item rootItem = root.Database.GetRootItem();
                if ((rootItem != null) && (root.ID != rootItem.ID))
                {
                    string str = root.Paths.Path;
                    if (path.StartsWith(str, StringComparison.InvariantCulture))
                    {
                        path = StringUtil.Mid(path, str.Length);
                    }
                }
            }
            return Assert.ResultNotNull<string>(path);
        }

        protected Language ContentLanguage
        {
            get
            {
                Language contentLanguage;
                if (!Language.TryParse(WebUtil.GetQueryString("la"), out contentLanguage))
                {
                    contentLanguage = Context.ContentLanguage;
                }
                return contentLanguage;
            }
        }

        protected string Mode
        {
            get
            {
                return Assert.ResultNotNull<string>(StringUtil.GetString(base.ServerProperties["Mode"], "shell"));
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                base.ServerProperties["Mode"] = value;
            }
        }

        protected bool UploadButtonDisabled
        {
            get
            {
                bool flag;
                return (bool.TryParse(StringUtil.GetString(base.ServerProperties["UploadButtonDisabled"], "false"), out flag) && flag);
            }
            set
            {
                if (value != this.UploadButtonDisabled)
                {
                    base.ServerProperties["UploadButtonDisabled"] = value;
                    string javascript = "var uploadButton = document.getElementById(\"{0}\");\r\n                                    if (uploadButton){{\r\n                                        uploadButton.disabled = {1};\r\n                                    }}".FormatWith(new object[] { this.Upload.UniqueID, value.ToString().ToLowerInvariant() });
                    if (Context.Page.Page.IsPostBack)
                    {
                        SheerResponse.Eval(javascript);
                    }
                    else
                    {
                        Context.Page.Page.ClientScript.RegisterStartupScript(base.GetType(), "UploadButtonModification", javascript, true);
                    }
                }
            }
        }
    }
}
