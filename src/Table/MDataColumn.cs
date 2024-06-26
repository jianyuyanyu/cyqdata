using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using CYQ.Data.SQL;
using CYQ.Data.Tool;
using System.IO;
using CYQ.Data.Json;

namespace CYQ.Data.Table
{
    /// <summary>
    /// 头列表集合
    /// </summary>
    public partial class MDataColumn
    {
        List<MCellStruct> structList;
        internal MDataTable _Table;
        internal MDataColumn(MDataTable table)
        {
            structList = new List<MCellStruct>();
            _Table = table;
        }

        public MDataColumn()
        {
            structList = new List<MCellStruct>();
        }
        /// <summary>
        /// 是否需要刷新索引
        /// </summary>
        internal bool IsNeedRefleshIndex = false;
       
        private int _CheckDuplicateState = -1;
        /// <summary>
        /// 添加列时，检测名称是否重复(默认为true)。
        /// </summary>
        public bool CheckDuplicate
        {
            get
            {
                //return true;
                if (_CheckDuplicateState == -1)
                {
                    return structList.Count < 100;//列多时，会影响性能，默认超过100条后，不检测重复项。
                }
                return _CheckDuplicateState == 1;
            }
            set
            {
                _CheckDuplicateState = value ? 1 : 0;
            }
        }
        /// <summary>
        /// 隐式转换列头
        /// </summary>
        /// <param name="columns"></param>
        /// <returns></returns>
        public static implicit operator MDataColumn(DataColumnCollection columns)
        {
            if (columns == null)
            {
                return null;
            }
            MDataColumn mColumns = new MDataColumn();

            if (columns.Count > 0)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    MCellStruct cellStruct = new MCellStruct(columns[i].ColumnName, DataType.GetSqlType(columns[i].DataType), columns[i].ReadOnly, columns[i].AllowDBNull, columns[i].MaxLength);
                    mColumns.Add(cellStruct);
                }
            }
            return mColumns;
        }

        public MCellStruct this[string key]
        {
            get
            {
                int index = GetIndex(key);
                if (index > -1)
                {
                    return this[index];
                }
                return null;
            }
        }
        /// <summary>
        /// 架构所引用的表
        /// </summary>
        [JsonIgnore]
        public MDataTable Table
        {
            get
            {
                return _Table;
            }
        }
        private string _Description = string.Empty;
        /// <summary>
        /// 表名描述
        /// </summary>
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;
            }
        }
        private string _TableName = string.Empty;
        /// <summary>
        /// 表名
        /// </summary>
        public string TableName
        {
            get
            {
                return _TableName;
            }
            set
            {
                if (!string.IsNullOrEmpty(_TableName) && _TableName != value)
                {
                    //外部修改了表名
                    for (int i = 0; i < this.Count; i++)
                    {
                        this[i].TableName = value;
                    }
                }
                _TableName = value;
            }
        }



        public MDataColumn Clone()
        {
            MDataColumn mcs = new MDataColumn();
            mcs.DataBaseType = DataBaseType;
            mcs.DataBaseVersion = DataBaseVersion;
            mcs.CheckDuplicate = false;
            mcs.isViewOwner = isViewOwner;
            mcs.TableName = TableName;
            mcs.Description = Description;
            foreach (string item in RelationTables)
            {
                mcs.AddRelateionTableName(item);
            }
            for (int i = 0; i < this.Count; i++)
            {
                mcs.Add(this[i].Clone());
            }
            return mcs;
        }
        public bool Contains(string columnName)
        {
            return GetIndex(columnName) > -1;
        }

        #region 索引获取与更新
        /// <summary>
        /// 存储列名的索引
        /// </summary>
        private Dictionary<string, int> columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 更新索引
        /// </summary>
        internal void RefleshIndex()
        {
            IsNeedRefleshIndex = false;
            if (Count == 0) { return; }
            Dictionary<string, int> newIndexs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Count; i++)
            {
                string name = this[i].ColumnName;
                newIndexs.Add(name, i);
                if (name.IndexOf('_') > -1)
                {
                    name = name.Replace("_", "");
                    newIndexs.Add(name, i);
                }
            }
            this.columnIndex = newIndexs;
        }

        /// <summary>
        /// 获取列所在的索引位置(若不存在返回-1）
        /// </summary>
        public int GetIndex(string columnName)
        {
            if (IsNeedRefleshIndex)
            {
                RefleshIndex();
            }
            if (!string.IsNullOrEmpty(columnName))
            {
                if (columnIndex.ContainsKey(columnName))
                {
                    return columnIndex[columnName];
                }
                if (columnName.Contains("_"))
                {
                    columnName = columnName.Replace("_", "");//兼容映射处理
                    if (columnIndex.ContainsKey(columnName))
                    {
                        return columnIndex[columnName];
                    }
                }
            }
            return -1;
        }
        #endregion
        /// <summary>
        /// 将 列 的序号或位置更改为指定的序号或位置。
        /// </summary>
        /// <param name="columnName">列名</param>
        /// <param name="ordinal">序号</param>
        public void SetOrdinal(string columnName, int ordinal)
        {
            int index = GetIndex(columnName);
            if (index > -1 && index != ordinal)
            {
                MCellStruct mstruct = this[index];
                if (_Table != null && _Table.Rows.Count > 0)
                {
                    List<object> items = _Table.GetColumnItems<object>(index, BreakOp.None);
                    _Table.Columns.RemoveAt(index);
                    _Table.Columns.Insert(ordinal, mstruct);
                    for (int i = 0; i < items.Count; i++)
                    {
                        _Table.Rows[i].Set(ordinal, items[i]);
                    }
                    items = null;
                }
                else
                {
                    structList.RemoveAt(index);//移除
                    if (ordinal >= Count)
                    {
                        ordinal = Count;
                    }
                    structList.Insert(ordinal, mstruct);
                }
            }
            IsNeedRefleshIndex = true;
        }
        /// <summary>
        /// 给列批量赋相同的值。
        /// </summary>
        /// <param name="columnName">列名</param>
        /// <param name="value">值</param>
        public void SetValue(string columnName, object value)
        {
            if (Contains(columnName))
            {
                this[columnName].Set(value);
            }
        }
        /// <summary>
        /// 输出Json格式的表构架
        /// </summary>
        public string ToJson(bool isFullSchema)
        {
            JsonHelper helper = new JsonHelper();
            helper.Fill(this, isFullSchema);
            return helper.ToString();
        }
        /// <summary>
        /// 转成行
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public MDataRow ToRow(string tableName)
        {
            MDataRow row = new MDataRow(this);
            row.TableName = tableName;
            //row.Columns.TableName = tableName;
            //row.Columns.CheckDuplicate = CheckDuplicate;
            //row.Columns.DataBaseType = DataBaseType;
            //row.Columns.DataBaseVersion = DataBaseVersion;
            //row.Columns.isViewOwner = isViewOwner;
            //row.Columns.RelationTables = RelationTables;
            row.Conn = Conn;
            return row;
        }
        /// <summary>
        /// 保存表架构到外部文件中(json格式）
        /// </summary>
        public bool WriteSchema(string fileName)
        {
            string schema = ToJson(true).Replace("},{", "},\r\n{");//写入文本时要换行。
            return IOHelper.Write(fileName, schema);
        }

        private List<MCellStruct> _JointPrimary = new List<MCellStruct>();
        /// <summary>
        /// 联合主键
        /// </summary>
        public List<MCellStruct> JointPrimary
        {
            get
            {
                MCellStruct autoIncrementCell = null;
                if (_JointPrimary.Count == 0 && this.Count > 0)
                {
                    foreach (MCellStruct item in this)
                    {
                        if (item.IsPrimaryKey)
                        {
                            _JointPrimary.Add(item);
                        }
                        else if (item.IsAutoIncrement)
                        {
                            autoIncrementCell = item;
                        }
                    }
                    if (_JointPrimary.Count == 0)
                    {
                        if (autoIncrementCell != null)
                        {
                            _JointPrimary.Add(autoIncrementCell);
                        }
                        else
                        {
                            _JointPrimary.Add(this[0]);
                        }
                    }
                }
                return _JointPrimary;
            }
        }

        /// <summary>
        /// 第一个主键
        /// </summary>
        public MCellStruct FirstPrimary
        {
            get
            {
                if (JointPrimary.Count > 0)
                {
                    return JointPrimary[0];
                }
                return null;
            }
        }
        /// <summary>
        /// 首个唯一键
        /// </summary>
        public MCellStruct FirstUnique
        {
            get
            {
                MCellStruct ms = null;
                foreach (MCellStruct item in this)
                {
                    if (item.IsUniqueKey)
                    {
                        return item;
                    }
                    else if (ms == null && !item.IsPrimaryKey && DataType.GetGroup(item.SqlType) == 0)//取第一个字符串类型
                    {
                        ms = item;
                    }
                }
                if (ms == null && this.Count > 0)
                {
                    ms = this[0];
                }
                return ms;
            }
        }

        /// <summary>
        /// 当前的数据库类型。
        /// </summary>
        internal DataBaseType DataBaseType = DataBaseType.None;
        /// <summary>
        /// 当前的数据库版本号。
        /// </summary>
        internal string DataBaseVersion = string.Empty;
        /// <summary>
        /// 当前的数据库链接项（或语句）
        /// </summary>
        internal string Conn = string.Empty;
        /// <summary>
        /// 该结构是否由视图拥有
        /// </summary>
        internal bool isViewOwner = false;

        internal List<string> relationTables = new List<string>();
        internal List<string> RelationTables
        {
            get
            {
                if (relationTables.Count == 0 && !string.IsNullOrEmpty(TableName))
                {
                    relationTables.Add(TableName);
                }
                return relationTables;
            }
            set
            {
                relationTables = value;
            }
        }
        internal void AddRelateionTableName(string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                string[] items = TableName.Split(',');
                foreach (string name in items)
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    if (!relationTables.Contains(tableName))
                    {
                        relationTables.Add(tableName);
                    }
                }
            }
        }

        /// <summary>
        /// 将表结构的数据转成Table显示
        /// </summary>
        /// <returns></returns>
        public MDataTable ToTable()
        {
            string tableName = string.Empty;
            if (_Table != null)
            {
                tableName = _Table.TableName;
            }
            MDataTable dt = new MDataTable(tableName);
            dt.Columns.Add("ColumnName,DataType,SqlType,MaxSize,Scale");
            dt.Columns.Add("IsPrimaryKey,IsAutoIncrement,IsCanNull,IsUniqueKey,IsForeignKey", SqlDbType.Bit);
            dt.Columns.Add("TableName,FKTableName,DefaultValue,Description");

            for (int i = 0; i < Count; i++)
            {
                MCellStruct ms = this[i];
                dt.NewRow(true)
                     .Sets(0, ms.ColumnName, ms.ValueType.Name, ms.SqlType, ms.MaxSize, ms.Scale)
                     .Sets(5, ms.IsPrimaryKey, ms.IsAutoIncrement, ms.IsCanNull, ms.IsUniqueKey, ms.IsForeignKey)
                     .Sets(10, ms.TableName, ms.FKTableName, ms.DefaultValue, ms.Description);
            }
            return dt;
        }


    }
    public partial class MDataColumn : IList<MCellStruct>
    {
        public int Count
        {
            get { return structList.Count; }
        }

        #region Add重载方法
        /// <summary>
        /// 添加列
        /// </summary>
        /// <param name="columnName">列名</param>
        public void Add(string columnName)
        {
            Add(columnName, SqlDbType.NVarChar, false, true, -1, false, null);
        }
        /// <param name="SqlType">列的数据类型</param>
        public void Add(string columnName, SqlDbType sqlType)
        {
            Add(columnName, sqlType, false, true, -1, false, null);
        }
        /// <param name="isAutoIncrement">是否自增id列</param>
        public void Add(string columnName, SqlDbType sqlType, bool isAutoIncrement)
        {
            Add(columnName, sqlType, isAutoIncrement, !isAutoIncrement, -1, isAutoIncrement, null);
        }

        public void Add(string columnName, SqlDbType sqlType, bool isAutoIncrement, bool isCanNull, int maxSize)
        {
            Add(columnName, sqlType, isAutoIncrement, isCanNull, maxSize, false, null);
        }
        public void Add(string columnName, SqlDbType sqlType, bool isAutoIncrement, bool isCanNull, int maxSize, bool isPrimaryKey, object defaultValue)
        {
            Add(columnName, sqlType, isAutoIncrement, isCanNull, maxSize, false, defaultValue, -1);
        }
        /// <param name="defaultValue">默认值[日期类型请传入SqlValue.GetDate]</param>
        public void Add(string columnName, SqlDbType sqlType, bool isAutoIncrement, bool isCanNull, int maxSize, bool isPrimaryKey, object defaultValue, short scale)
        {
            string[] items = columnName.Split(',');
            foreach (string item in items)
            {
                MCellStruct mdcStruct = new MCellStruct(item, sqlType, isAutoIncrement, isCanNull, maxSize);
                mdcStruct.Scale = scale;
                mdcStruct.IsPrimaryKey = isPrimaryKey;
                mdcStruct.DefaultValue = defaultValue;
                Add(mdcStruct);
            }
        }

        #endregion

        public void Add(MCellStruct item)
        {
            if (item != null && !this.Contains(item) && (!CheckDuplicate || !Contains(item.ColumnName)))//
            {
                if (DataBaseType == DataBaseType.None)
                {
                    DataBaseType = item.DalType;
                }
                item.MDataColumn = this;
                structList.Add(item);
                if (_Table != null && _Table.Rows.Count > 0)
                {
                    for (int i = 0; i < _Table.Rows.Count; i++)
                    {
                        if (Count > _Table.Rows[i].Count)
                        {
                            _Table.Rows[i].Add(new MDataCell(ref item));
                        }
                    }
                }
                IsNeedRefleshIndex = true;
            }
        }


        //public void AddRange(IEnumerable<MCellStruct> collection)
        //{
        //    AddRange(collection as MDataColumn);
        //}
        public void AddRange(MDataColumn items)
        {
            if (items.Count > 0)
            {
                foreach (MCellStruct item in items)
                {
                    if (!Contains(item.ColumnName))
                    {
                        Add(item);
                    }
                }
                IsNeedRefleshIndex = true;
            }
        }


        public bool Remove(MCellStruct item)
        {
            Remove(item.ColumnName);
            return true;
        }
        public void Remove(string columnName)
        {
            string[] items = columnName.Split(',');
            foreach (string item in items)
            {
                int index = GetIndex(item);
                if (index > -1)
                {
                    RemoveAt(index);
                    IsNeedRefleshIndex = true;
                }
            }
        }

        public void RemoveRange(int index, int count) // 1,4
        {
            for (int i = index; i < index + count; i++)
            {
                RemoveAt(index);//每次删除都移动索引，所以连续删除N次即可。
            }
        }
        public void RemoveAt(int index)
        {
            structList.RemoveAt(index);
            if (_Table != null)
            {
                foreach (MDataRow row in _Table.Rows)
                {
                    if (row.Count > Count)
                    {
                        row.RemoveAt(index);
                    }
                }
            }
            IsNeedRefleshIndex = true;
        }


        public void Insert(int index, MCellStruct item)
        {
            if (item != null && !this.Contains(item) && (!CheckDuplicate || !Contains(item.ColumnName)))// 
            {
                item.MDataColumn = this;
                structList.Insert(index, item);
                if (_Table != null && _Table.Rows.Count > 0)
                {
                    for (int i = 0; i < _Table.Rows.Count; i++)
                    {
                        if (Count > _Table.Rows[i].Count)
                        {
                            _Table.Rows[i].Insert(index, new MDataCell(ref item));
                        }
                    }
                }
                IsNeedRefleshIndex = true;
            }

        }
        public void InsertRange(int index, MDataColumn mdc)
        {
            for (int i = mdc.Count; i >= 0; i--)
            {
                Insert(index, mdc[i]);//反插
            }
        }

        #region IList<MCellStruct> 成员

        int IList<MCellStruct>.IndexOf(MCellStruct item)
        {
            return structList.IndexOf(item);
        }

        #endregion

        #region ICollection<MCellStruct> 成员

        void ICollection<MCellStruct>.CopyTo(MCellStruct[] array, int arrayIndex)
        {
            structList.CopyTo(array, arrayIndex);
        }


        bool ICollection<MCellStruct>.IsReadOnly
        {
            get { return false; }
        }

        #endregion

        #region IEnumerable<MCellStruct> 成员

        IEnumerator<MCellStruct> IEnumerable<MCellStruct>.GetEnumerator()
        {
            return structList.GetEnumerator();
        }

        #endregion

        #region IEnumerable 成员

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return structList.GetEnumerator();
        }

        #endregion

        #region ICollection<MCellStruct> 成员


        public void Clear()
        {
            structList.Clear();
        }

        public bool Contains(MCellStruct item)
        {
            return structList.Contains(item);
        }

        #endregion

        #region IList<MCellStruct> 成员

        /// <summary>
        /// ReadOnly
        /// </summary>
        public MCellStruct this[int index]
        {
            get
            {
                return structList[index];
            }
            set
            {
                Error.Throw(AppConst.Global_NotImplemented);
            }
        }

        #endregion
    }
    public partial class MDataColumn
    {
        /// <summary>
        /// 从Json或文件中加载成列信息
        /// </summary>
        /// <param name="jsonOrFileName">Json或文件（完整路径）名称</param>
        /// <returns></returns>
        public static MDataColumn CreateFrom(string jsonOrFileName)
        {
            return CreateFrom(jsonOrFileName, true);
        }
        /// <summary>
        /// 从Json或文件中加载成列信息
        /// </summary>
        /// <param name="jsonOrFileName">Json或文件（完整路径）名称</param>
        /// <param name="readTxtOrXml">是否从.txt或.xml文件中读取架构（默认为true）</param>
        /// <returns></returns>
        public static MDataColumn CreateFrom(string jsonOrFileName, bool readTxtOrXml)
        {
            if (string.IsNullOrEmpty(jsonOrFileName))
            {
                return null;
            }
            MDataColumn mdc = new MDataColumn();

            MDataTable dt = null;
            try
            {
                bool isTxtOrXml = false;
                string json = string.Empty;
                char c = jsonOrFileName[0];
                bool isJson = c == '{' || c == '[' || c == '<';
                string exName = null;
                string fileName = null;
                if (!isJson)
                {
                    exName = Path.GetExtension(jsonOrFileName);
                    fileName = Path.GetFileNameWithoutExtension(jsonOrFileName);
                    switch (exName.ToLower())
                    {
                        case ".ts":
                        case ".xml":
                        case ".txt":
                            string tsFileName = jsonOrFileName.Replace(exName, ".ts");
                            if (File.Exists(tsFileName))
                            {
                                json = IOHelper.ReadAllText(tsFileName);
                            }
                            else if (readTxtOrXml && File.Exists(jsonOrFileName))
                            {
                                isTxtOrXml = true;
                                if (exName == ".xml")
                                {
                                    json = IOHelper.ReadAllText(jsonOrFileName, 0, Encoding.UTF8);
                                }
                                else if (exName == ".txt")
                                {
                                    json = IOHelper.ReadAllText(jsonOrFileName);
                                }
                            }

                            break;
                        default:
                            json = jsonOrFileName;
                            break;
                    }
                }
                else
                {
                    json = jsonOrFileName;
                }
                if (!string.IsNullOrEmpty(json))
                {
                    dt = MDataTable.CreateFrom(json);
                    if (dt.TableName == MDataTable.DefaultTableName && !string.IsNullOrEmpty(fileName))
                    {
                        dt.TableName = fileName;
                    }
                    if (dt.Columns.Count > 0)
                    {
                        if (isTxtOrXml)
                        {
                            mdc = dt.Columns.Clone();
                        }
                        else
                        {
                            foreach (MDataRow row in dt.Rows)
                            {
                                MCellStruct cs = new MCellStruct(
                                    row.Get<string>("ColumnName"),
                                    DataType.GetSqlType(row.Get<string>("SqlType", "string")),
                                    row.Get<bool>("IsAutoIncrement", false),
                                    row.Get<bool>("IsCanNull", false),
                                    row.Get<int>("MaxSize", -1));
                                cs.Scale = row.Get<short>("Scale");
                                cs.IsPrimaryKey = row.Get<bool>("IsPrimaryKey", false);
                                cs.DefaultValue = row.Get<string>("DefaultValue");


                                //新增属性
                                cs.Description = row.Get<string>("Description");
                                cs.TableName = row.Get<string>("TableName");
                                cs.IsUniqueKey = row.Get<bool>("IsUniqueKey", false);
                                cs.IsForeignKey = row.Get<bool>("IsForeignKey", false);
                                cs.FKTableName = row.Get<string>("FKTableName");
                                cs.SqlTypeName = row.Get<string>("SqlTypeName");
                                mdc.Add(cs);
                            }
                            mdc.TableName = dt.TableName;
                            mdc.Description = dt.Description;
                            mdc.relationTables = dt.Columns.relationTables;
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Log.Write(err, LogType.Error);
            }
            finally
            {
                dt = null;
            }
            return mdc;
        }

        /// <summary>
        /// 处理.ts 文件里的Json
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        //private static MDataColumn CreateFromByTsJson(string json)
        //{
        //    MDataColumn mdc = new MDataColumn();
        //}
    }
}
