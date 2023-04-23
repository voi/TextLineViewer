// https://chivsp.hatenablog.com/entry/2018/02/26/090000
//
// @set MYPATH=%WinDir%\Microsoft.NET\Framework\v4.0.30319
// @set PATH=%MYPATH%;%PATH%
// @csc.exe /t:winexe /optimize+ /out:TextLineViewer.exe TextLineViewer.cs /r:system.dll,system.drawing.dll,system.windows.forms.dll,system.io.dll,System.Reflection.dll
// @set MYPATH=
//
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

namespace TextLineViewer
{
    public class LineViewForm : Form
    {
        //
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new LineViewForm());
        }

        //
        class DisplayFormatter
        {
            class DisplayFormat
            {
                private Regex pattern_;
                private string format_;

                public DisplayFormat(XmlElement format)
                {
                    this.pattern_ = new Regex(format.GetAttribute("pattern"), RegexOptions.Compiled);
                    this.format_ = format.GetAttribute("format");
                }

                public bool Apply(ref string text)
                {
                    if(this.pattern_.IsMatch(text))
                    {
                        text = this.pattern_.Replace(text, this.format_);
                    }

                    return false;
                }
            }

            private List<DisplayFormat> formats_;

            public DisplayFormatter(XmlNodeList nodes)
            {
                this.formats_ = new List<DisplayFormat>();

                if(nodes == null)
                {
                    return;
                }

                foreach(var node in nodes)
                {
                    formats_.Add(new DisplayFormat((XmlElement)node));
                }
            }

            public string Apply(string source_text)
            {
                string text = source_text;

                foreach(var format in this.formats_)
                {
                    if(format.Apply(ref text))
                    {
                        break;
                    }
                }

                return text;
            }
        }

        //
        class LineItem
        {
            public string Text { get; private set; }
            public string Display { get; private set; }

            public LineItem()
            {
                this.Text = String.Empty;
                this.Display = String.Empty;
            }

            public LineItem(string line, DisplayFormatter formatter)
            {
                this.Text = line;
                this.Display = formatter.Apply(line);
            }

            public bool IsMatch(string pattern)
            {
                return Regex.IsMatch(this.Text, pattern);
            }

            public override string ToString()
            {
                return this.Display;
            }
        }

        //
        interface IContents
        {
            IList<LineItem> Lines { get; }
            void Filtering(string pattern);
        }

        //
        class Contents : IContents
        {
            private List<LineItem> lines_;
            private List<LineItem> filtering_cache_;

            public Contents()
            {
                this.lines_ = new List<LineItem>();
                this.filtering_cache_ = new List<LineItem>();
            }

            public Contents(Encoding encoding, string file_path, DisplayFormatter formatter) : this()
            {
                if(File.Exists(file_path))
                {
                    this.lines_.AddRange(Contents.GetLines(file_path, encoding, formatter));
                    this.filtering_cache_.AddRange(this.lines_);
                }
            }

            public IList<LineItem> Lines
            {
                get
                {
                    return this.filtering_cache_;
                }
            }

            public void Filtering(string pattern)
            {
                this.filtering_cache_.Clear();
                this.filtering_cache_.AddRange(
                    this.lines_.Where((item) => {
                        return item.IsMatch(pattern);
                    }));
            }
            
            protected void Add(LineItem item)
            {
                this.lines_.Add(item);
            }

            protected void Save(string file_path, Encoding encoding)
            {
                if(this.lines_.Count() <= 0)
                {
                    return;
                }

                using(var writer = new StreamWriter(file_path, false, encoding, 4096))
                {
                    foreach(var item in this.lines_)
                    {
                        writer.WriteLine(item.Text);
                    }
                }
            }

            private static IEnumerable<LineItem> GetLines(
                string file_path, Encoding encoding, DisplayFormatter formatter)
            {
                if(File.Exists(file_path))
                {
                    using(var reader = new StreamReader(file_path, encoding))
                    {
                        while(reader.Peek() > 0)
                        {
                            yield return new LineItem(reader.ReadLine(), formatter);
                        }
                    }
                }
            }
        }

        //
        class CacheContents : Contents
        {
            private Encoding encoding_;
            private string file_path_;

            public CacheContents()
            {
                this.encoding_ = Encoding.Default;
                this.file_path_ = String.Empty;
            }

            public CacheContents(
                Encoding encoding, string file_path, DisplayFormatter formatter)
                : base(encoding, file_path, formatter)
            {
                this.encoding_ = encoding;
                this.file_path_ = file_path;
            }

            public void AddItem(LineItem item)
            {
                base.Add(item);
            }

            public void Save()
            {
                base.Save(this.file_path_, this.encoding_);
            }
        }

        //
        class Context
        {
            public Contents Source { get; private set; }
            public CacheContents MruCache { get; private set; }
            public string ActionProcess { get; private set; }
            public string ActionArgs { get; private set; }

            private DisplayFormatter formatter_;

            public Context()
            {
                this.Source = new Contents();
                this.MruCache = new CacheContents();
                this.formatter_ = new DisplayFormatter(null);
            }

            public Context(XmlDocument config_document) : this()
            {
                this.formatter_ = new DisplayFormatter(config_document.SelectNodes(@"/configure/formats/format"));
                this.LoadContents((XmlElement)config_document.SelectSingleNode(@"/configure/content"));
                this.LoadActionInfo((XmlElement)config_document.SelectSingleNode(@"/configure/action"));
            }

            private void LoadContents(XmlElement node)
            {
                if(node == null)
                {
                    return ;
                }

                //
                var encoding = this.GetEncoding(node.GetAttribute("encoding")) ;

                //
                var source_path = node.GetAttribute("path");
                source_path = Path.GetFullPath(source_path);

                this.Source = new Contents(encoding, source_path, this.formatter_);
                this.MruCache = new CacheContents(encoding, source_path + ".cache", this.formatter_);
            }

            private Encoding GetEncoding(string code_name)
            {
                var code_page = 0;
                Encoding encoding;

                if(Int32.TryParse(code_name, out code_page))
                {
                    encoding = Encoding.GetEncoding(code_page);
                }
                else
                {
                    encoding = Encoding.GetEncoding(code_name);
                }

                if(encoding == null)
                {
                    encoding = Encoding.UTF8;
                }

                return encoding;
            }

            private void LoadActionInfo(XmlElement node)
            {
                if(node != null)
                {
                    this.ActionProcess = node.GetAttribute("process");
                    this.ActionArgs = node.GetAttribute("args");
                }
            }
        }

        //
        class ActionForSelectedItemEventArgs : EventArgs
        {
            public LineItem Item { get; private set; }

            public ActionForSelectedItemEventArgs(LineItem item)
            {
                this.Item = item;
            }
        }

        //
        class SelecterTabPage : TabPage
        {
            TableLayoutPanel layout_page_;
            ListBox listbox_show_;
            TextBox textbox_filtering_;

            IContents contents_;
            BindingList<LineItem> binding_list_;

            public event EventHandler<ActionForSelectedItemEventArgs> DoActionForSelectedItem;

            public SelecterTabPage(string name) : base(name)
            {
                //
                this.listbox_show_ = new System.Windows.Forms.ListBox();
                this.listbox_show_.Dock = System.Windows.Forms.DockStyle.Fill;
                this.listbox_show_.KeyDown += new KeyEventHandler(this.ListBoxShow_KeyDown);
                this.listbox_show_.DoubleClick += new EventHandler(this.ListBoxShow_DoubleClick);

                //
                this.textbox_filtering_ = new System.Windows.Forms.TextBox();
                this.textbox_filtering_.Dock = System.Windows.Forms.DockStyle.Fill;
                this.textbox_filtering_.TextChanged += new EventHandler(this.TextBoxFiltering_TextChanged);

                //
                this.layout_page_ = new System.Windows.Forms.TableLayoutPanel();
                this.layout_page_.Dock = System.Windows.Forms.DockStyle.Fill;
                this.layout_page_.ColumnCount = 1;
                this.layout_page_.RowCount = 2;

                this.layout_page_.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                this.layout_page_.RowStyles.Add(new RowStyle(SizeType.Percent, 90F));
                this.layout_page_.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                this.layout_page_.Controls.Add(this.listbox_show_, 0, 0);
                this.layout_page_.Controls.Add(this.textbox_filtering_, 0, 1);

                //
                this.Controls.Add(this.layout_page_);
            }

            private void ListBoxShow_KeyDown(Object sender, KeyEventArgs e)
            {
                if(e.KeyData == Keys.Enter)
                {
                    this.OnActionForSelectedItem();
                }
            }

            private void ListBoxShow_DoubleClick(Object sender, EventArgs e)
            {
                this.OnActionForSelectedItem();
            }

            private void TextBoxFiltering_TextChanged(Object sender, EventArgs e)
            {
                this.UpdateView();
            }

            private void OnActionForSelectedItem()
            {
                var item = (LineItem)this.listbox_show_.SelectedItem;

                if((item != null) && (this.DoActionForSelectedItem != null))
                {
                    this.DoActionForSelectedItem(this, new ActionForSelectedItemEventArgs(item));
                }
            }

            public void BindContents(IContents content)
            {
                this.contents_ = content;
                this.binding_list_ = new BindingList<LineItem>(content.Lines);
                this.listbox_show_.DataSource = this.binding_list_;
            }

            public void UpdateView()
            {
                if(this.contents_ != null)
                {
                    this.contents_.Filtering(this.textbox_filtering_.Text);
                    this.binding_list_.ResetBindings();
                }
            }
        }

        //
        TabControl tab_lists_;
        SelecterTabPage tabpage_mru_;
        SelecterTabPage tabpage_all_;

        //
        Context context_;

        //
        public LineViewForm()
        {
            ComponentInitialize();
        }

        private void ComponentInitialize()
        {
            //
            this.tabpage_mru_ = new SelecterTabPage("MRU Lines");
            this.tabpage_mru_.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabpage_mru_.DoActionForSelectedItem += this.TabPageMru_DoActionForSelectedItem;

            //
            this.tabpage_all_ = new SelecterTabPage("All Lines");
            this.tabpage_all_.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabpage_all_.DoActionForSelectedItem += this.TabPageAll_DoActionForSelectedItem;

            //
            this.tab_lists_ = new TabControl();
            this.tab_lists_.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tab_lists_.Controls.Add(this.tabpage_mru_);
            this.tab_lists_.Controls.Add(this.tabpage_all_);

            //
            this.Controls.Add(this.tab_lists_);
            this.Size = new Size(this.Size.Width * 2, this.Size.Height * 5 / 2);

            foreach(var font_family in FontFamily.Families)
            {
                if(Regex.IsMatch(font_family.Name, @"Meiryo UI"))
                {
                    this.Font = new System.Drawing.Font(font_family, 13F);
                    break;
                }
            }

            this.Load += new EventHandler(this.TextLineViewer_Load);
            this.Closed += new EventHandler(this.TextLineViewer_Closed);
        }

        private void TextLineViewer_Load(Object sender, EventArgs e)
        {
            XmlDocument document = new XmlDocument();
            var args = Environment.GetCommandLineArgs();

            if(args.Length > 1)
            {
                document.Load(args[1]);
            }
            else
            {
                if(File.Exists("textlineviewer.config"))
                {
                    document.Load("textlineviewer.config");
                }
            }

            if(document.DocumentElement == null)
            {
                this.context_ = new Context();
            }
            else
            {
                this.context_ = new Context(document);
            }

            //
            this.tabpage_mru_.BindContents(this.context_.MruCache);
            this.tabpage_all_.BindContents(this.context_.Source);

            //
            if(this.context_.MruCache.Lines.Count() <= 0)
            {
                this.tab_lists_.SelectedIndex = 1;
            }
        }

        private void TextLineViewer_Closed(Object sender, EventArgs e)
        {
            this.context_.MruCache.Save();
        }

        private void TabPageMru_DoActionForSelectedItem(Object sender, ActionForSelectedItemEventArgs e)
        {
            this.ActionForSelectedItem(e.Item);
        }

        private void TabPageAll_DoActionForSelectedItem(Object sender, ActionForSelectedItemEventArgs e)
        {
            this.context_.MruCache.AddItem(e.Item);
            this.tabpage_mru_.UpdateView();
            this.ActionForSelectedItem(e.Item);
        }

        private void ActionForSelectedItem(LineItem item)
        {
            ProcessStartInfo start_info = new ProcessStartInfo();
            string line = item.Text.Replace("\"", "\\\"");

            if(this.context_.ActionArgs.IndexOf("{0}") >= 0)
            {
                start_info.Arguments = String.Format(this.context_.ActionArgs, line);
            }
            else
            {
                start_info.Arguments = String.Format("{0} \"{1}\"", this.context_.ActionArgs, line);
            }

            start_info.FileName = this.context_.ActionProcess;
            start_info.WindowStyle = ProcessWindowStyle.Hidden;
            start_info.ErrorDialog = true;

            if(Regex.IsMatch(start_info.FileName, @"^\s*$"))
            {
                return ;
            }

            Process.Start(start_info);
        }
    }
}
