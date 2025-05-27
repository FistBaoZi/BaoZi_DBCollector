using BaoZi.Tools.DBHelper;
using Newtonsoft.Json.Linq;

namespace dbcollector_api.Services
{
    public class ChangeTrackingDataCollector
    {
        private readonly IBaseDBHelper _sourceDbHelper; // 用于连接源数据库（只读）
        private readonly IBaseDBHelper _targetDbHelper; // 用于连接目标数据库（读写）
        private readonly string _sourceTableName;
        public readonly string _targetTableName;
        private readonly string _primaryKeyColumn;
        public readonly string _siteId;
        private long _lastSyncVersion; // 用于存储上次同步的 Change Tracking 版本
        private const string VERSION_TABLE_NAME = "ChangeTrackingVersions";
        private const string FULL_SYNC_STATE_TABLE = "FullSyncState";

        public ChangeTrackingDataCollector(IBaseDBHelper sourceDbHelper, IBaseDBHelper targetDbHelper, string sourceTableName, string targetTableName, string primaryKeyColumn, string siteId)
        {
            _sourceDbHelper = sourceDbHelper;
            _targetDbHelper = targetDbHelper;
            _sourceTableName = sourceTableName;
            _targetTableName = targetTableName;
            _primaryKeyColumn = primaryKeyColumn;
            _siteId = siteId;
            _lastSyncVersion = GetLastSyncVersionFromConfig();
        }

        private void EnsureVersionTableExists()
        {
            string sql = $@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{VERSION_TABLE_NAME}')
                BEGIN
                    CREATE TABLE {VERSION_TABLE_NAME} (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        SiteId VARCHAR(50) NOT NULL,
                        TableName VARCHAR(100) NOT NULL,
                        Version BIGINT NOT NULL,
                        CreateTime DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_{VERSION_TABLE_NAME}_SiteId_Table ON {VERSION_TABLE_NAME}(SiteId, TableName, CreateTime);
                END";
            _targetDbHelper.Update(sql);
        }

        private void EnsureFullSyncStateTableExists()
        {
            string sql = $@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{FULL_SYNC_STATE_TABLE}')
                BEGIN
                    CREATE TABLE {FULL_SYNC_STATE_TABLE} (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        SiteId VARCHAR(50) NOT NULL,
                        TableName VARCHAR(100) NOT NULL,
                        LastSyncId BIGINT NOT NULL,
                        IsCompleted BIT NOT NULL DEFAULT 0,
                        CreateTime DATETIME NOT NULL DEFAULT GETDATE(),
                        UpdateTime DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_{FULL_SYNC_STATE_TABLE}_Key ON {FULL_SYNC_STATE_TABLE}(SiteId, TableName);
                END";
            _targetDbHelper.Update(sql);
        }

        private void CleanupOldVersions()
        {
            string sql = $@"
                DELETE FROM {VERSION_TABLE_NAME} 
                WHERE SiteId = @siteId 
                AND TableName = @tableName
                AND CreateTime < DATEADD(day, -3, GETDATE())";
            _targetDbHelper.Delete(sql,
                new Parameter("@siteId", _siteId),
                new Parameter("@tableName", _sourceTableName));
        }

        private long GetLastSyncVersionFromConfig()
        {
            EnsureVersionTableExists();
            string sql = $@"
                SELECT TOP 1 Version 
                FROM {VERSION_TABLE_NAME} 
                WHERE SiteId = @siteId 
                AND TableName = @tableName
                ORDER BY CreateTime DESC";
            var result = _targetDbHelper.FindOne(sql,
                new Parameter("@siteId", _siteId),
                new Parameter("@tableName", _sourceTableName));
            if (result.Result != null && result.Result["Version"] != null)
            {
                return result.Result["Version"].ToObject<long>();
            }
            return 0;
        }

        private void SaveCurrentSyncVersionToConfig(long currentVersion)
        {
            EnsureVersionTableExists();
            CleanupOldVersions();

            string sql = $@"
                    INSERT INTO {VERSION_TABLE_NAME} (SiteId, TableName, Version,CreateTime)
                    VALUES (@siteId, @tableName, @version,getdate())";

            _targetDbHelper.Insert(sql,
                new Parameter("@siteId", _siteId),
                new Parameter("@tableName", _sourceTableName),
                new Parameter("@version", currentVersion));

            _lastSyncVersion = currentVersion;
        }

        private bool TargetTableExists()
        {
            string sql = $"IF OBJECT_ID('dbo.{_targetTableName}', 'U') IS NOT NULL SELECT 1 as havetable  ELSE SELECT 0 as havetable ;";
            var result = _targetDbHelper.FindOne(sql);
            return result.Result["havetable"].ToObject<int>() == 1;
        }

        private bool TargetTableContainsSiteData()
        {
            if (!TargetTableExists())
            {
                return false;
            }
            string sql = $"SELECT TOP 1 1 as havedata FROM {_targetTableName} WHERE bz_ct_siteid = @siteId;";
            var result = _targetDbHelper.FindOne(sql, new Parameter("@siteId", _siteId));
            return result.Result["havedata"].ToObject<int>() == 1;
        }

        /// <summary>
        /// 获取源表的结构并进行类型转换。
        /// </summary>
        private Dictionary<string, string> GetSourceTableSchema()
        {
            string sql = $@"SELECT COLUMN_NAME, DATA_TYPE
                           FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = @tableName;";
            var result = _sourceDbHelper.Find<JObject>(sql, new Parameter("@tableName", _sourceTableName));
            var schemaMap = new Dictionary<string, string>();
            if (result.Result != null)
            {
                foreach (var row in result.Result)
                {
                    string columnName = row.Value<string>("COLUMN_NAME");
                    string dataType = row.Value<string>("DATA_TYPE").ToLower();

                    switch (dataType)
                    {
                        case "int":
                            schemaMap[columnName] = "INT";
                            break;
                        case "smallint":
                        case "tinyint":
                        case "float":
                        case "real":
                        case "numeric":
                        case "money":
                        case "smallmoney":
                            schemaMap[columnName] = "DECIMAL(18, 2)";
                            break;
                        case "varchar":
                        case "nvarchar":
                        case "char":
                        case "nchar":
                        case "text":
                        case "ntext":
                            schemaMap[columnName] = "NVARCHAR(MAX)";
                            break;
                        case "datetime":
                        case "datetime2":
                        case "smalldatetime":
                            schemaMap[columnName] = "DATETIME";
                            break;
                        case "bit":
                            schemaMap[columnName] = "BIT";
                            break;
                        case "bigint":
                            schemaMap[columnName] = "BIGINT";
                            break;
                        default:
                            schemaMap[columnName] = "NVARCHAR(MAX)"; // 保留其他类型，可以根据需要添加更多转换
                            break;
                    }
                }
            }
            return schemaMap;
        }

        /// <summary>
        /// 获取目标表的当前结构
        /// </summary>
        private Dictionary<string, string> GetTargetTableSchema()
        {
            string sql = $@"SELECT COLUMN_NAME, DATA_TYPE 
                           FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = @tableName;";
            var result = _targetDbHelper.Find<JObject>(sql, new Parameter("@tableName", _targetTableName));
            var schemaMap = new Dictionary<string, string>();
            
            if (result.Result != null)
            {
                foreach (var row in result.Result)
                {
                    string columnName = row.Value<string>("COLUMN_NAME");
                    string dataType = row.Value<string>("DATA_TYPE").ToLower();
                    schemaMap[columnName] = dataType;
                }
            }
            return schemaMap;
        }

        /// <summary>
        /// 检查并更新表结构
        /// </summary>
        private void SyncTableSchema()
        {
            if (!TargetTableExists())
            {
                var sourceSchema2 = GetSourceTableSchema();
                CreateTargetTable(sourceSchema2);
                return;
            }

            var sourceSchema = GetSourceTableSchema();
            var targetSchema = GetTargetTableSchema();

            // 使用不区分大小写的字段比较
            var newColumns = sourceSchema
                .Where(s => !targetSchema.Keys.Any(t => t.Equals(s.Key, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (newColumns.Any())
            {
                Console.WriteLine($"发现 {newColumns.Count} 个新字段，开始同步表结构...");
                foreach (var col in newColumns)
                {
                    string alterSql = $"ALTER TABLE {_targetTableName} ADD {col.Key} {col.Value};";
                    try 
                    {
                        var result = _targetDbHelper.Update(alterSql);
                        if (string.IsNullOrWhiteSpace(result.Message))
                        {
                            Console.WriteLine($"成功添加字段: {col.Key} {col.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"添加字段失败 {col.Key}: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"添加字段失败 {col.Key}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 创建目标表，并在原有结构的基础上添加 siteid 字段，并使用规范化的数据类型。
        /// </summary>
        private void CreateTargetTable(Dictionary<string, string> sourceColumnSchema)
        {
            // 检查表是否存在
            if (!TargetTableExists())
            {
                // 表不存在时创建表
                List<string> columnDefinitions = new List<string>();
                foreach (var kvp in sourceColumnSchema)
                {
                    columnDefinitions.Add($"{kvp.Key} {kvp.Value}");
                }
                columnDefinitions.Add("bz_ct_siteid VARCHAR(50)"); // 添加带前缀的站点ID字段

                string createTableSql = $"CREATE TABLE {_targetTableName} ({string.Join(",", columnDefinitions)});";
                var result = _targetDbHelper.Update(createTableSql);
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    Console.WriteLine($"创建目标表 {_targetTableName} 失败: {result.Message}");
                }
                else
                {
                    Console.WriteLine($"目标表 {_targetTableName} 创建成功。");
                }
                return;
            }

            // 表已存在，获取目标表当前结构
            var targetSchema = GetTargetTableSchema();

            // 使用 StringComparer.OrdinalIgnoreCase 进行不区分大小写的字段比较
            var missingColumns = sourceColumnSchema
                .Where(s => !targetSchema.Keys.Any(t => t.Equals(s.Key, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // 添加缺失的字段
            if (missingColumns.Any())
            {
                Console.WriteLine($"在目标表 {_targetTableName} 中发现 {missingColumns.Count} 个新字段，开始添加...");
                foreach (var col in missingColumns)
                {
                    string alterSql = $"ALTER TABLE {_targetTableName} ADD {col.Key} {col.Value};";
                    try
                    {
                        var result = _targetDbHelper.Update(alterSql);
                        if (string.IsNullOrWhiteSpace(result.Message))
                        {
                            Console.WriteLine($"成功添加字段: {col.Key} {col.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"添加字段失败 {col.Key}: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"添加字段失败 {col.Key}: {ex.Message}");
                    }
                }
            }
        }

        private long GetCurrentChangeTrackingVersion()
        {
            string sql = "SELECT CHANGE_TRACKING_CURRENT_VERSION() AS CurrentVersion;";
            var result = _sourceDbHelper.FindOne<JObject>(sql);
            if (result.Result != null && result.Result.ContainsKey("CurrentVersion"))
            {
                return result.Result.Value<long>("CurrentVersion");
            }
            else
            {
                Console.WriteLine($"获取当前 Change Tracking 版本失败 (SiteId: {_siteId}): {result.Message}");
                return -1;
            }
        }

        private JArray GetChanges()
        {
            if (_lastSyncVersion == 0)
            {
                Console.WriteLine($"尚未进行首次同步 (SiteId: {_siteId})。");
                return GetAllSourceData();
            }

            string sql = $@"
                SELECT CT.SYS_CHANGE_OPERATION, CT.{_primaryKeyColumn}, T.*
                FROM CHANGETABLE(CHANGES {_sourceTableName}, {_lastSyncVersion}) AS CT
                LEFT JOIN {_sourceTableName} AS T ON CT.{_primaryKeyColumn} = T.{_primaryKeyColumn};";

            var result = _sourceDbHelper.Find(sql);
            if (result.Result != null)
            {
                return result.Result;
            }
            else
            {
                Console.WriteLine($"查询变更失败 (SiteId: {_siteId}): {result.Message}");
                return new JArray();
            }
        }

        private JArray GetAllSourceData(long lastId = 0, int pageSize = 1000)
        {
            string sql = $@"SELECT TOP {pageSize} * 
                           FROM {_sourceTableName} 
                           WHERE {_primaryKeyColumn} > @lastId 
                           ORDER BY {_primaryKeyColumn}";
            var result = _sourceDbHelper.Find(sql, new Parameter("@lastId", lastId));
            return result.Result ?? new JArray();
        }

        private void BulkInsertData(JArray dataToInsert)
        {
            if (!dataToInsert.Any())
            {
                return;
            }

            // 在插入数据前先检查并更新表结构
            SyncTableSchema();

            // 获取最新的表结构（包含可能新增的列）
            List<string> targetColumns = new List<string>(GetSourceTableSchema().Keys);
            targetColumns.Add("bz_ct_siteid");

            // 构建更新和插入SQL
            List<string> updateColumns = targetColumns.Where(c => c != _primaryKeyColumn && c != "bz_ct_siteid")
                                                    .Select(c => $"{c} = @{c}")
                                                    .ToList();
            string updateSql = $@"UPDATE {_targetTableName} 
                                 SET {string.Join(",", updateColumns)}
                                 WHERE {_primaryKeyColumn} = @{_primaryKeyColumn} 
                                 AND bz_ct_siteid = @bz_ct_siteid";

            List<string> valuePlaceholders = targetColumns.Select(c => "@" + c).ToList();
            string insertSql = $"INSERT INTO {_targetTableName} ({string.Join(",", targetColumns)}) VALUES ({string.Join(",", valuePlaceholders)});";

            foreach (var data in dataToInsert)
            {
                try
                {
                    string checkSql = $"SELECT 1 FROM {_targetTableName} WHERE {_primaryKeyColumn} = @primaryKey AND bz_ct_siteid = @siteId";
                    var checkParams = new[] {
                        new Parameter("@primaryKey", data[_primaryKeyColumn]?.ToString()),
                        new Parameter("@siteId", _siteId)
                    };
                    var checkResult = _targetDbHelper.FindOne(checkSql, checkParams);

                    List<Parameter> parameters = new List<Parameter>();
                    foreach (var column in targetColumns.Where(c => c != "bz_ct_siteid"))
                    {
                        var value = data[column];
                        object paramValue;
                        
                        if (value == null || value.Type == JTokenType.Null || 
                            (value.Type == JTokenType.String && string.IsNullOrWhiteSpace(value.ToString())))
                        {
                            paramValue = DBNull.Value;
                        }
                        else
                        {
                            paramValue = value.ToString();
                        }
                        
                        parameters.Add(new Parameter("@" + column, paramValue));
                    }
                    parameters.Add(new Parameter("@bz_ct_siteid", _siteId));

                    if (checkResult.Result != null)
                    {
                        var updateResult = _targetDbHelper.Update(updateSql, parameters.ToArray());
                        if (!string.IsNullOrWhiteSpace(updateResult.Message))
                        {
                            Console.WriteLine($"更新数据失败 (SiteId: {_siteId}, 主键: {data[_primaryKeyColumn]}): {updateResult.Message}");
                        }
                    }
                    else
                    {
                        var insertResult = _targetDbHelper.Insert(insertSql, parameters.ToArray());
                        if (!string.IsNullOrWhiteSpace(insertResult.Message))
                        {
                            Console.WriteLine($"插入数据失败 (SiteId: {_siteId}, 主键: {data[_primaryKeyColumn]}): {insertResult.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理数据失败 (SiteId: {_siteId}, 主键: {data[_primaryKeyColumn]}): {ex.Message}");
                    // TODO: 考虑记录失败数据或进行重试
                }
            }
            Console.WriteLine($"成功处理 {dataToInsert.Count} 条数据到 {_targetTableName} (SiteId: {_siteId})。");
        }

        private (bool needFullSync, long lastSyncId) CheckFullSyncStatus()
        {
            EnsureFullSyncStateTableExists();
            
            string sql = $@"
                SELECT LastSyncId, IsCompleted 
                FROM {FULL_SYNC_STATE_TABLE}
                WHERE SiteId = @siteId 
                AND TableName = @tableName";
                
            var result = _targetDbHelper.FindOne(sql,
                new Parameter("@siteId", _siteId),
                new Parameter("@tableName", _sourceTableName));

            if (result.Result == null) // 从未开始过全量同步
            {
                return (true, 0);
            }

            bool isCompleted = result.Result["IsCompleted"].ToObject<bool>();
            long lastSyncId = result.Result["LastSyncId"].ToObject<long>();

            return (!isCompleted, lastSyncId); // 如果未完成,则需要续采
        }

        private void UpdateFullSyncStatus(long lastSyncId, bool isCompleted)
        {
            string sql = $@"
                MERGE {FULL_SYNC_STATE_TABLE} AS target
                USING (SELECT @siteId as SiteId, @tableName as TableName) AS source
                ON target.SiteId = source.SiteId AND target.TableName = source.TableName
                WHEN MATCHED THEN
                    UPDATE SET LastSyncId = @lastSyncId, IsCompleted = @isCompleted, UpdateTime = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (SiteId, TableName, LastSyncId, IsCompleted, CreateTime, UpdateTime)
                    VALUES (@siteId, @tableName, @lastSyncId, @isCompleted, GETDATE(), GETDATE());";

            _targetDbHelper.Update(sql,
                new Parameter("@siteId", _siteId),
                new Parameter("@tableName", _sourceTableName),
                new Parameter("@lastSyncId", lastSyncId),
                new Parameter("@isCompleted", isCompleted));
        }

        public void CollectData()
        {
            // 在采集数据前先同步表结构
            SyncTableSchema();

            // 1. 检查是否需要全量采集或续采
            var (needFullSync, lastSyncId) = CheckFullSyncStatus();

            if (needFullSync)
            {
                Console.WriteLine($"开始{(lastSyncId == 0 ? "全量采集" : "续采")}厂站 {_siteId}{_sourceTableName}的数据。上次同步ID: {lastSyncId}");
                
                // 确保目标表存在
                var sourceTableSchema = GetSourceTableSchema();
                CreateTargetTable(sourceTableSchema);

                // 分页获取并同步数据
                int totalCount = 0;
                const int pageSize = 20;
                
                while (true)
                {
                    var pageData = GetAllSourceData(lastSyncId, pageSize);
                    if (!pageData.Any())
                    {
                        // 全量同步完成,更新状态为已完成
                        UpdateFullSyncStatus(lastSyncId, true);
                        break;
                    }

                    BulkInsertData(pageData);
                    totalCount += pageData.Count;
                    lastSyncId = pageData.Last().Value<long>(_primaryKeyColumn);
                    
                    // 更新同步状态但标记为未完成
                    UpdateFullSyncStatus(lastSyncId, false);
                    
                    Console.WriteLine($"厂站 {_siteId}{_sourceTableName} 已完成 {totalCount} 条数据采集,当前ID: {lastSyncId}");
                    System.Threading.Thread.Sleep(5000);
                }

                // 更新Change Tracking版本
                long currentVersion = GetCurrentChangeTrackingVersion();
                SaveCurrentSyncVersionToConfig(currentVersion);
                Console.WriteLine($"厂站 {_siteId}{_sourceTableName}的全量采集完成，共采集 {totalCount} 条数据。");
            }
            else 
            {
                // 1. 检查目标表是否存在或是否包含当前厂站的数据
                bool targetTableExists = TargetTableExists();
                bool containsSiteData = TargetTableContainsSiteData();

                // 2. 如果是第一次采集该厂站的数据
                if (!targetTableExists || !containsSiteData)
                {
                    Console.WriteLine($"首次采集厂站 {_siteId}{_sourceTableName}的数据或目标表不存在，执行全量采集并创建/更新目标表结构。");
                    // a. 获取源表结构并进行类型转换
                    var sourceTableSchema = GetSourceTableSchema();
                    // b. 创建目标表 (如果不存在)
                    CreateTargetTable(sourceTableSchema);
                    // c. 分页获取全量源数据并写入
                    long lastId = 0;
                    int totalCount = 0;
                    const int pageSize = 1000;
                    
                    while (true)
                    {
                        var pageData = GetAllSourceData(lastId, pageSize);
                        if (!pageData.Any())
                        {
                            break;
                        }

                        // d. 写入目标表并标记 siteid
                        BulkInsertData(pageData);
                        
                        totalCount += pageData.Count;
                        // 获取最后一条记录的ID作为下次查询的起点
                        lastId = pageData.Last().Value<long>(_primaryKeyColumn);
                        
                        Console.WriteLine($"厂站 {_siteId}{_sourceTableName} 已完成 {totalCount} 条数据采集");
                        
                        // 等待20秒后继续下一页采集
                        System.Threading.Thread.Sleep(20000);
                    }

                    // e. 更新 Change Tracking 版本
                    long currentVersion = GetCurrentChangeTrackingVersion();
                    SaveCurrentSyncVersionToConfig(currentVersion);
                    Console.WriteLine($"厂站 {_siteId}{_sourceTableName}的首次全量采集完成，共采集 {totalCount} 条数据。");
                }
                else
                {
                    Console.WriteLine($"开始增量采集厂站 {_siteId}{_sourceTableName}的数据。");
                    // a. 查询变更数据
                    var changes = GetChanges();


                    if (changes.Any())
                    {
                        JArray dataToInsert = new JArray();
                        foreach (var change in changes)
                        {
                            string operation = change.Value<string>("SYS_CHANGE_OPERATION");
                            if (operation == "I" || operation == "U")
                            {
                                JObject rowData = new JObject();
                                foreach (var property in JObject.FromObject(change).Properties().Where(p => p.Name != "SYS_CHANGE_OPERATION"))
                                {
                                    rowData[property.Name] = property.Value;
                                }
                                dataToInsert.Add(rowData);
                            }
                            else if (operation == "D")
                            {
                                JValue primaryKeyValue = change.Value<JValue>(_primaryKeyColumn);
                                Console.WriteLine($"厂站 {_siteId}{_sourceTableName}删除记录 (主键: {primaryKeyValue})");
                                string delsql = $"delete from {_targetTableName} where bz_ct_siteid='{_siteId}' and {_primaryKeyColumn} = '{primaryKeyValue}'";
                                 _targetDbHelper.Delete(delsql);
                            }
                        }
                        // b. 批量插入/更新数据到目标表
                        if (dataToInsert.Any())
                        {
                            BulkInsertData(dataToInsert);
                        }
                        // 获取并保存当前版本号
                        long currentVersion = GetCurrentChangeTrackingVersion();
                        SaveCurrentSyncVersionToConfig(currentVersion);
                        Console.WriteLine($"厂站 {_siteId}{_sourceTableName}的增量采集完成，处理了 {changes.Count} 条变更。");
                    }
                    else
                    {
                        Console.WriteLine($"厂站 {_siteId}{_sourceTableName}没有新的数据变更。");
                    }
                }
            }
        }

        /// <summary>
        /// 检查数据采集的一致性
        /// </summary>
        /// <returns>返回true表示数据一致，false表示数据不一致</returns>
        private bool CheckDataConsistency()
        {
            // 获取目标表最后两条记录的ID
            string targetSql = $@"
                SELECT TOP 2 {_primaryKeyColumn}
                FROM {_targetTableName}
                WHERE bz_ct_siteid = @siteId
                ORDER BY {_primaryKeyColumn} DESC";

            // 获取源表最后两条记录的ID
            string sourceSql = $@"
                SELECT TOP 2 {_primaryKeyColumn}
                FROM {_sourceTableName}
                ORDER BY {_primaryKeyColumn} DESC";

            try
            {
                var targetResult = _targetDbHelper.Find(targetSql, new Parameter("@siteId", _siteId));
                var sourceResult = _sourceDbHelper.Find(sourceSql);

                if (targetResult.Result == null || sourceResult.Result == null || 
                    targetResult.Result.Count < 2 || sourceResult.Result.Count < 2)
                {
                    Console.WriteLine($"警告：表 {_sourceTableName} (SiteId: {_siteId}) 记录数不足，无法进行一致性检查");
                    return true;
                }

                var targetSecondLastId = targetResult.Result[1][_primaryKeyColumn].ToObject<long>();
                var sourceSecondLastId = sourceResult.Result[1][_primaryKeyColumn].ToObject<long>();

                if (targetSecondLastId != sourceSecondLastId)
                {
                    Console.WriteLine($"警告：表 {_sourceTableName} (SiteId: {_siteId}) 数据采集异常");
                    Console.WriteLine($"源表倒数第二条记录ID: {sourceSecondLastId}");
                    Console.WriteLine($"目标表倒数第二条记录ID: {targetSecondLastId}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查数据一致性时发生错误 (SiteId: {_siteId}): {ex.Message}");
                return false;
            }
        }
    }
}
