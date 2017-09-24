﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Firefly.Box;

using Firefly.Box.Data.Advanced;
using ENV.Utilities;
using Firefly.Box.Advanced;
using Firefly.Box.Data;
using System.IO;
using System.Xml;
using Firefly.Box.Testing;

namespace ENV.Web
{
    public class ViewModelHelper
    {
        private const string optionalUrlParametersHtmlDoc = @"
<strong>Optional Url Parameters</strong>
<ul>
    <li><strong>_limit</strong> - Num of rows per result</li>
    <li><strong>_page</strong> - Page Number</li>
    <li><strong>_sort</strong> - Sort Columns</li>
    <li><strong>_order</strong> - Sort Direction</li>
    <li><strong>_gt, _gte, _lt, _lte, _ne</strong> - Filter Data Options</li>
</ul>";
        protected readonly ENV.UserMethods u;
        public ViewModelHelper()
        {
            u = UserMethods.Instance;
            _bp.Load += OnLoad;
        }
        protected virtual void OnLoad() { }
        public ViewModelHelper(Firefly.Box.Data.Entity e, bool allowInsertUpdateDelete = false) : this()
        {
            From = e;
            if (allowInsertUpdateDelete)
            {
                AllowInsertUpdateDelete();
            }

        }
        internal void AssertColumnKey(ColumnBase c, string key)
        {
            _colMap[c].AssertKey(key);
        }
        protected void MapExperssion(string name, Func<Text> exp)
        {
            MapColumn(Columns.Add(new TextColumn(name)).BindValue(exp));

        }
        protected void AddAllColumns()
        {
            _bp.AddAllColumns();
        }
        protected void AllowInsertUpdateDelete()
        {
            AllowUpdate = true;
            AllowDelete = true;
            AllowInsert = true;
        }

        BusinessProcess _bp = new BusinessProcess() { TransactionScope = TransactionScopes.Task };

        bool _init = false;
        FilterCollection _tempFilter = new FilterCollection();
        protected RelationCollection Relations { get { return _bp.Relations; } }
        protected FilterCollection Where { get { return _bp.Where; } }
        protected Sort OrderBy { get { return _bp.OrderBy; } }
        protected internal ColumnCollection Columns => _bp.Columns;
        protected bool AllowUpdate { get; set; }
        protected bool AllowDelete { get; set; }
        protected bool AllowInsert { get; set; }
        protected internal Firefly.Box.Data.Entity From { get { return _bp.From; } set { _bp.From = value; } }

        protected virtual void OnInsert() { }
        protected virtual void OnUpdate() { }
        void init()
        {
            if (_init)
                return;
            _init = true;
            if (_columns.Count == 0)
            {
                if (_bp.Columns.Count == 0)
                    _bp.AddAllColumns();
                MapColumns(_bp.Columns);
            }
            if (!_colMap.ContainsKey(_idColumn))
            {
                MapColumns(new[] { _idColumn });
            }
            _bp.Where.Add(_tempFilter);

        }
        internal static ContextStatic<IMyHttpContext> HttpContext = new ContextStatic<IMyHttpContext>(() => new HttpContextBridgeToIHttpContext(System.Web.HttpContext.Current));
        public DataList GetRows()
        {
            init();
            var dl = new DataList();

            foreach (var item in _colsPerKey)
            {
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key), _tempFilter, new equalToFilter());
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key + "_gt"), _tempFilter, new greater());
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key + "_gte"), _tempFilter, new greaterEqual());
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key + "_lt"), _tempFilter, new lesser());
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key + "_lte"), _tempFilter, new lessOrEqual());
                item.Value.addFilter(HttpContext.Value.GetRequestParam(item.Key + "_ne"), _tempFilter, new different());
            }
            long start = 0;
            long numOfRows = 25;
            {
                var limit = HttpContext.Value.GetRequestParam("_limit");
                if (!string.IsNullOrEmpty(limit))
                    numOfRows = Number.Parse(limit);
                if (Number.IsNullOrZero(numOfRows))
                    numOfRows = 25;
            }
            if (numOfRows > 0)
            {
                var page = HttpContext.Value.GetRequestParam("_page");
                if (!string.IsNullOrEmpty(page))
                {

                    var x = Number.Parse(page);
                    if (x > 0)
                        start = (x - 1) * numOfRows;
                }
            }
            var ob = _bp.OrderBy;
            var sort = HttpContext.Value.GetRequestParam("_sort");
            if (!string.IsNullOrEmpty(sort))
            {
                var orderBy = new Sort();
                var s = sort.Split(',');
                var ord = HttpContext.Value.GetRequestParam("_order") ?? "";
                var o = ord.Split(',');
                for (int i = 0; i < s.Length; i++)
                {
                    ColumnInViewModel cvm;
                    if (_colsPerKey.TryGetValue(s[i], out cvm))
                    {
                        var so = SortDirection.Ascending;
                        if (o.Length > i && o[i].ToLower().StartsWith("d"))
                            so = SortDirection.Descending;
                        cvm.AddSort(orderBy, so);
                    }
                }
                if (orderBy.Segments.Count > 0)
                    _bp.OrderBy = orderBy;
            }
            try
            {
                _bp.ForEachRow(() =>
                {
                    if (_bp.Counter > start)
                        dl.AddItem(GetItem());
                    if (_bp.Counter == start + numOfRows)
                        _bp.Exit();

                });
                return dl;
            }
            finally
            {
                _bp.OrderBy = ob;
                _tempFilter.Clear();
            }
        }
        public void Describe(TextWriter tw, string name)
        {
            tw.WriteLine("export class " + name + " extends entity {");
            init();
            foreach (var item in _columns)
            {
                tw.WriteLine("    " + item.Key + " = new stringColumn('" + item.Caption + "');");
            }
            tw.WriteLine(@"    constructor(ds?: dataSource) {
        super(ds ? ds : shared.server, '" + name + @"');
        this.initColumns(this.id);
    }");
            tw.WriteLine("}");
        }
        public void CreateTypeScriptClass(TextWriter tw, string name)
        {
            tw.WriteLine("export interface " + NameFixer.MakeSingular(name) + " {");
            init();
            foreach (var item in _columns)
            {
                tw.WriteLine("    " + item.Key + "?:string;");
            }
            tw.WriteLine("}");
        }
        public void ColumnList(TextWriter tw)
        {
            init();
            tw.WriteLine("columnKeys:[");
            bool first = true;
            foreach (var item in _columns)
            {
                if (first)
                    first = false;
                else
                    tw.Write(",");
                tw.Write("\"" + item.Key + "\"");
            }
            tw.WriteLine("]");
        }
        public void FullColumnList(TextWriter tw)
        {
            init();
            tw.WriteLine("columnSettings:[");
            bool first = true;
            foreach (var item in _columns)
            {
                if (first)
                    first = false;
                else
                    tw.WriteLine(",");
                tw.Write("{key:\"" + item.Key + "\",caption:\"" + item.Caption + "\"}");
            }
            tw.WriteLine();
            tw.WriteLine("]");
        }
        DataItem GetItem()
        {
            if (!_init)
                throw new InvalidOperationException("Init was not run");
            var x = new DataItem();
            foreach (var item in _columns)
            {
                item.SaveTo(x);
            }
            return x;
        }
        void forId(string id, Action what, Activities activity = Activities.Update, Action onEnd = null)
        {
            try
            {
                init();
                if (_bp.From.PrimaryKeyColumns.Length == 1)
                {
                    var x = new equalToFilter();
                    Caster.Cast(_idColumn, id, x);
                    _tempFilter.Add(x.result);
                }
                else
                {
                    var sr = new SeperatedReader(id);
                    int i = 0;
                    foreach (var item in _bp.From.PrimaryKeyColumns)
                    {
                        var x = new equalToFilter();
                        Caster.Cast(item, sr[i++], x);
                        _tempFilter.Add(x.result);

                    }
                }
                if (onEnd != null)
                    _bp.End += onEnd;
                _bp.Activity = activity;
                what();
            }
            finally
            {
                _bp.Activity = Activities.Update;
                if (onEnd != null)
                    _bp.End -= onEnd;

                _tempFilter.Clear();
            }
        }
        public DataItem GetRow(string id)
        {
            DataItem result = null;
            forId(id, () =>
            {
                _bp.ForFirstRow(() =>
                {
                    result = GetItem();

                });
            });
            return result;
        }
        public void Delete(string id)
        {
            forId(id, () =>
            {
                _bp.ForFirstRow(() => { });
            }, Activities.Delete);
        }
        public DataItem Update(string id, DataItem item)
        {
            DataItem result = null;
            forId(id, () =>
            {

                _bp.ForFirstRow(() =>
                {
                    foreach (var c in _columns)
                    {
                        c.UpdateDataBasedOnItem(item);
                    }
                    OnUpdate();
                });

            }, Activities.Update, () => result = GetItem());
            return result;
        }

        public DataItem Insert(DataItem item)
        {
            init();
            DataItem result = null;
            Action onEnd = () =>
              {
                  result = GetItem();
              };
            try
            {
                _bp.Activity = Activities.Insert;

                _bp.End += onEnd;
                _bp.ForFirstRow(() =>
                {
                    foreach (var c in _columns)
                    {
                        c.UpdateDataBasedOnItem(item);
                    }
                    OnInsert();
                });
                return result;
            }
            finally
            {
                _bp.Activity = Activities.Update;
                _bp.End -= onEnd;
            }
        }

        class ColumnInViewModel
        {
            string _key;
            Func<object> _getValueFromRow;
            Action<DataItemValue> _setValueBasedOnDataItem;
            ColumnBase _col;
            public ColumnInViewModel(string key, Func<object> getValue, Action<DataItemValue> setValue, ColumnBase col)
            {
                _col = col;
                _key = key;
                _getValueFromRow = getValue;
                _setValueBasedOnDataItem = setValue;
            }

            internal void addFilter(string dataItemValue, FilterCollection tempFilter, filterAbstract f)
            {
                if (dataItemValue == null)
                    return;

                Caster.Cast(_col, dataItemValue, f);
                if (f.result != null)
                {
                    tempFilter.Add(f.result);
                }
            }


            internal void AddSort(Sort orderBy, SortDirection so)
            {
                orderBy.Add(_col, so);
            }

            internal void AssertKey(string key)
            {
                _key.ShouldBe(key);
            }
            public string Key => _key;
            public string Caption => _col.Caption;


            internal void SaveTo(DataItem x)
            {
                x.Set(_key, _getValueFromRow());
            }

            internal void UpdateDataBasedOnItem(DataItem item)
            {
                _setValueBasedOnDataItem(item[_key]);
            }
        }
        List<ColumnInViewModel> _columns = new List<ColumnInViewModel>();
        Dictionary<ColumnBase, ColumnInViewModel> _colMap = new Dictionary<ColumnBase, ColumnInViewModel>();
        Dictionary<string, ColumnInViewModel> _colsPerKey = new Dictionary<string, ColumnInViewModel>();
        bool _handledIdentity = false;
        ColumnBase _idColumn;
        internal protected void MapColumn(params ColumnBase[] columns)
        {
            MapColumns(columns);
        }
        void MapColumns(IEnumerable<ColumnBase> columns)
        {

            if (!_handledIdentity)
            {
                _handledIdentity = true;
                if (_bp.From==null )
                    throw new NotImplementedException("Must have an Entity - did you forget to set the From");
                if (_bp.From.PrimaryKeyColumns == null || _bp.From.PrimaryKeyColumns.Length == 0)
                    throw new NotImplementedException("Entity must have a primary key");

                if (_bp.From.PrimaryKeyColumns.Length == 1)
                {
                    _idColumn = _bp.From.PrimaryKeyColumns[0];

                }
                else
                {
                    _idColumn = new TextColumn("id").BindValue(() => {

                        var x = new SeperatedBuilder();
                        foreach (var item in _bp.From.PrimaryKeyColumns)
                        {
                            x.Add(DataItem.FixValueTypes(item.Value));
                        }
                        return x.ToString();

                    });
                    Columns.Add(_bp.From.PrimaryKeyColumns);
                    Columns.Add(_idColumn);
                    

                }
                    
            }
            foreach (var column in columns)
            {
                string name = column.Caption;
                if (column == _idColumn)
                    name = "id";
                name = NameFixer.fixName(name);
                var orgName = name;
                int i = 1;
                while (_colsPerKey.ContainsKey(name))
                {
                    name = orgName + (i++).ToString();
                }
                var cv = new ColumnInViewModel(name, () => column.Value, v =>
                {
                    Caster.Cast(column, v, new setValueForColumn());
                }, column);
                _colMap.Add(column, cv);
                _colsPerKey.Add(name, cv);
                _columns.Add(cv);
            }
        }
      

        public static void RegisterViewModel(string key, Func<ViewModelHelper> controller)
        {
            _controllers.Add(key.ToLower(), controller);
        }
        public static void RegisterEntityByDbName(System.Type t, bool allowInsertUpdateDelete = false)
        {
            var e = ((ENV.Data.Entity)System.Activator.CreateInstance(t));
            RegisterEntity(e.EntityName, t, allowInsertUpdateDelete);
        }
        public static void RegisterEntity(string name, System.Type t, bool allowInsertUpdateDelete = false)
        {

            RegisterViewModel(name, () => new ViewModelHelper((ENV.Data.Entity)System.Activator.CreateInstance(t), allowInsertUpdateDelete));
        }
        public static void RegisterEntityByClassName(System.Type t)
        {

            RegisterEntity(t.Name, t);
        }
        static Dictionary<string, Func<ViewModelHelper>> _controllers = new Dictionary<string, Func<ViewModelHelper>>();
        public static void ProcessRequest(string name, string id = null)
        {
            try
            {
                var Response = System.Web.HttpContext.Current.Response;
                var Request = System.Web.HttpContext.Current.Request;
                Firefly.Box.Context.Current.SetNonUIThread();
                var responseType = (System.Web.HttpContext.Current.Request.Params["_response"] ?? "J").ToUpper();
                Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                Response.Headers.Add("Access-Control-Allow-Headers", "content-type");
                {
                    var x = Request["HTTP_ORIGIN"];
                    if (!string.IsNullOrWhiteSpace(x))
                        Response.Headers.Add("Access-Control-Allow-Origin", x);
                }
                {//fix id stuff
                    var url = Request.RawUrl;
                    var z = url.IndexOf('?');
                    if (z >= 0)
                        url = url.Remove(z);
                    var x = url.Split('/');
                    id = null;
                    if (x[x.Length - 1] != name)
                        id = x[x.Length - 1];
                }
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Func<ViewModelHelper> vmcFactory;
                    if (_controllers.TryGetValue(name.ToLower(), out vmcFactory))
                    {
                        var vmc = vmcFactory();
                        {
                            string jsonResult = null;
                            Response.ContentType = "application/json";
                            switch (Request.HttpMethod.ToLower())
                            {
                                case "get":
                                    using (var sw = new System.IO.StringWriter())
                                    {

                                        ISerializedObjectWriter w = new JsonISerializedObjectWriter(sw);
                                        if (responseType.StartsWith("X"))
                                        {
                                            w = new XmlISerializedObjectWriter(new XmlTextWriter(sw));
                                            Response.ContentType = "text/xml";
                                        }
                                        else if (responseType.StartsWith("C"))
                                        {
                                            w = new CSVISerializedObjectWriter(sw);
                                            Response.ContentType = "application/csv";
                                            Response.AddHeader("Content-Disposition", "attachment;filename=" + name + ".csv");
                                        }
                                        else if (responseType.StartsWith("H"))
                                        {
                                            ResponseIsHtml(Response);
                                            w = new HTMLISerializedObjectWriter(sw, name)
                                            {
                                                BodyAddition = optionalUrlParametersHtmlDoc
                                            };

                                        }
                                        if (responseType.StartsWith("D"))
                                        {
                                            Response.ContentType = "text/plain";
                                            sw.WriteLine("// /" + name + "?_responseType=" + responseType);
                                            sw.WriteLine();
                                            if (responseType.StartsWith("DE"))
                                                vmc.Describe(sw, name);
                                            else if (responseType.StartsWith("DCF"))
                                                vmc.FullColumnList(sw);
                                            else if (responseType.StartsWith("DC"))
                                                vmc.ColumnList(sw);
                                            else
                                                vmc.CreateTypeScriptClass(sw, name);
                                        }
                                        else if (string.IsNullOrEmpty(id))
                                            vmc.GetRows().ToWriter(w);
                                        else
                                            vmc.GetRow(id).ToWriter(w);
                                        w.Dispose();
                                        Response.Write(sw.ToString());
                                        break;
                                    }
                                case "post":
                                    if (!vmc.AllowInsert)
                                        throw new InvalidOperationException("Insert not allowed");
                                    Request.InputStream.Position = 0;
                                    using (var sr = new System.IO.StreamReader(Request.InputStream))
                                    {
                                        Response.Write(vmc.Insert(DataItem.FromJson(sr.ReadToEnd())).ToJson());
                                    }
                                    break;
                                case "put":
                                    if (!vmc.AllowUpdate)
                                        throw new InvalidOperationException("Update not allowed");
                                    Request.InputStream.Position = 0;
                                    using (var sr = new System.IO.StreamReader(Request.InputStream))
                                    {
                                        Response.Write(vmc.Update(id, DataItem.FromJson(sr.ReadToEnd())).ToJson());
                                    }
                                    break;
                                case "delete":
                                    if (!vmc.AllowDelete)
                                        throw new InvalidOperationException("Delete not allowed");
                                    vmc.Delete(id);
                                    break;
                                case "options":
                                    var allowedMethods = "GET,HEAD,PATCH";
                                    if (vmc.AllowUpdate)
                                        allowedMethods += ",PUT";
                                    if (vmc.AllowInsert)
                                        allowedMethods += ",POST";
                                    if (vmc.AllowDelete)
                                        allowedMethods += ",DELETE";
                                    Response.Headers.Add("Access-Control-Allow-Methods", allowedMethods);
                                    Response.StatusCode = 204;
                                    return;
                            }
                        
                        }
                    }
                }
                else
                {
                    ResponseIsHtml(Response);
                    Response.Write(HTMLISerializedObjectWriter.HTMLPageHeader);
                    Response.Write($"<h1>{Request.Path} Documentation</h1>");
                    foreach (var item in _controllers)
                    {

                        Response.Write("<h2>" + item.Key + "</h2>");
                        try
                        {
                            var c = item.Value();
                            Response.Write(@"<table class=""table table-responsive table-striped table-hover table-condensed table-responsive""><thead><tr><th>API</th><th><th></tr></thead><tbody>");
                            string url = Request.Path;
                            if (!url.EndsWith("/"))
                                url += "/";
                            url+= item.Key;
                            void addLine(string action, Action<Action<string, string>> addLink = null, bool dontNeedId = false)
                            {
                                var sw = new StringBuilder();
                                if (addLink != null)
                                {
                                    addLink((linkName, linkResponseType) =>
                                    {
                                        var linkUrl = url;
                                        if (!string.IsNullOrEmpty(linkResponseType))
                                            linkUrl += "?_response=" + linkResponseType;

                                        sw.Append($"<a href=\"{linkUrl}\">{linkName}</a> ");
                                    });
                                }

                                Response.Write($"<tr><td>{action} {url+(dontNeedId?"":"/{id}") }</td><td>{sw.ToString()}</td></tr>");
                            }
                            addLine("GET", x => {
                                x("JSON", "");
                                x("XML", "xml");
                                x("CSV", "csv");
                                x("HTML", "html");
                                x("ts interface", "d");
                                x("column list", "dc");
                                x("full column list", "dcf");
                            }, true);
                            addLine("GET");
                            if (c.AllowInsert)
                                addLine("POST");
                            if (c.AllowUpdate)
                                addLine("PUT");
                            if (c.AllowDelete)
                                addLine("DELETE");


                            Response.Write("</tbody></table>");
                        }
                        catch (Exception ex)
                        {
                            Response.Write("Error: " + ex.Message);
                        }

                    }
                    Response.Write(optionalUrlParametersHtmlDoc);
                }
            }
            finally
            {

            }
        }

        private static void ResponseIsHtml(System.Web.HttpResponse Response)
        {
            Response.ContentType = "text/html";
        }

      
    }
    class setValueForColumn : DoSomething
    {
        public void What<T>(TypedColumnBase<T> c, T val)
        {
            if (!Firefly.Box.Advanced.Comparer.Equal(c.Value, val))
                c.Value = val;
        }
    }


    interface DoSomething
    {
        void What<T>(TypedColumnBase<T> col, T val);
    }
    abstract class filterAbstract : DoSomething
    {
        public FilterBase result;
        public abstract void What<T>(TypedColumnBase<T> col, T val);
    }
    class equalToFilter : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsEqualTo(val);
        }
    }
    class greater : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsGreaterThan(val);
        }
    }
    class greaterEqual : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsGreaterOrEqualTo(val);
        }
    }
    class lesser : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsLessThan(val);
        }
    }
    class lessOrEqual : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsLessOrEqualTo(val);
        }
    }
    class different : filterAbstract
    {
        public override void What<T>(TypedColumnBase<T> col, T val)
        {
            result = col.IsDifferentFrom(val);
        }
    }

    class Caster : UserMethods.IColumnSpecifier
    {
        DataItemValue _v;
        public FilterBase result;
        DoSomething _ds;
        public static void Cast(ColumnBase c, DataItemValue v, DoSomething d)
        {
            UserMethods.CastColumn(c, new Caster(v, d));
        }
        public static void Cast(ColumnBase c, string v, DoSomething d)
        {
            UserMethods.CastColumn(c, new Caster(new DataItemValue(v), d));
        }
        public Caster(DataItemValue v, DoSomething ds)
        {
            _ds = ds;
            _v = v;
        }
        public void DoOnColumn(TypedColumnBase<Firefly.Box.Text> column)
        {
            _ds.What(column, _v.GetValue<Text>());
        }

        public void DoOnColumn(TypedColumnBase<Number> column)
        {
            _ds.What(column, _v.GetValue<Number>());
        }

        public void DoOnColumn(TypedColumnBase<Date> column)
        {
            _ds.What(column, _v.GetValue<Date>());
        }

        public void DoOnColumn(TypedColumnBase<Time> column)
        {
            _ds.What(column, _v.GetValue<Time>());
        }

        public void DoOnColumn(TypedColumnBase<Bool> column)
        {
            _ds.What(column, _v.GetValue<Bool>());
        }

        public void DoOnColumn(TypedColumnBase<byte[]> column)
        {
            throw new NotImplementedException();
        }

        public void DoOnUnknownColumn(ColumnBase column)
        {
            throw new NotImplementedException();
        }
    }
    
}
