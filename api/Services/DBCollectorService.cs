using BaoZi.Tools.DBHelper;
using BaoZi.Tools.TimerTask;
using dbcollector_api.Models;
using dbcollector_api.Utils;
using FluentScheduler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dbcollector_api.Services
{
    public class DBCollectorService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IBaseDBHelper DBCenter;
        private const string STATION_TABLE_NAME = "BZ_CT_StationConfig";
        private const string TABLE_CONFIG_NAME = "BZ_CT_CollectTableConfig";
        private const string FULL_SYNC_STATE_TABLE = "BZ_CT_FullSyncState";
        private readonly TimerTaskService TimerTaskService;
        private readonly IConfiguration _cfg;
        public DBCollectorService(IBaseDBHelper baseDBHelper, TimerTaskService timerTaskService, IConfiguration cfg, IWebHostEnvironment env)
        {
            DBCenter = baseDBHelper;
            TimerTaskService = timerTaskService;
            _cfg = cfg;
            _env = env;
        }

        public async Task<StationConfig> GetStationByIdAsync(int id)
        {
            string sql = $@"SELECT * FROM {STATION_TABLE_NAME} WHERE Id = @id";
            var result = await DBCenter.FindOneAsync<StationConfig>(sql, new Parameter("@id", id));
            return result.Result;
        }

        public async Task<StationConfig> GetStationByIdAsync(string stationId)
        {
            string sql = $@"SELECT * FROM {STATION_TABLE_NAME} WHERE StationId = @stationId";
            var result = await DBCenter.FindOneAsync<StationConfig>(sql, new Parameter("@stationId", stationId));
            return result.Result;
        }

        //添加采集厂站配置
        public async Task EnsureStationTableExistsAsync()
        {
            string sql = $@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{STATION_TABLE_NAME}')
                BEGIN
                    CREATE TABLE {STATION_TABLE_NAME} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        StationId VARCHAR(50) NOT NULL,
                        ConnectionString NVARCHAR(500) NOT NULL,
                        CreateTime DATETIME NOT NULL DEFAULT GETDATE(),
                        UpdateTime DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE UNIQUE INDEX IX_{STATION_TABLE_NAME}_StationId ON {STATION_TABLE_NAME}(StationId);
                END";
            await DBCenter.UpdateAsync(sql);
        }

        public List<StationConfig> GetStationList()
        {
            string sql = $"SELECT * FROM {STATION_TABLE_NAME} ORDER BY CreateTime DESC";
            var res =  DBCenter.Find<StationConfig>(sql);
            return res.Result;
        }

        public async Task AddStationAsync(StationConfig station)
        {
            string sql = $@"
                INSERT INTO {STATION_TABLE_NAME} (Name, StationId, ConnectionString, CreateTime, UpdateTime)
                VALUES (@name, @stationId, @connectionString, GETDATE(), GETDATE());
                SELECT SCOPE_IDENTITY() AS Id;";

            await DBCenter.InsertAsync(sql,
               new Parameter("@name", station.Name),
               new Parameter("@stationId", station.StationId),
               new Parameter("@connectionString", station.ConnectionString));
        }

        public async Task UpdateStationAsync(StationConfig station)
        {
            string sql = $@"
                UPDATE {STATION_TABLE_NAME} 
                SET Name = @name,
                    StationId = @stationId,
                    ConnectionString = @connectionString,
                    UpdateTime = GETDATE()
                WHERE Id = @id";

            await DBCenter.UpdateAsync(sql,
               new Parameter("@id", station.Id),
               new Parameter("@name", station.Name),
               new Parameter("@stationId", station.StationId),
               new Parameter("@connectionString", station.ConnectionString));
        }

        public async Task DeleteStationAsync(int id)
        {
            string sql = $"DELETE FROM {STATION_TABLE_NAME} WHERE Id = @id";
            await DBCenter.DeleteAsync(sql, new Parameter("@id", id));
        }

        public async Task EnsureTableConfigExists()
        {
            string sql = $@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{TABLE_CONFIG_NAME}')
                BEGIN
                    CREATE TABLE {TABLE_CONFIG_NAME} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        StationId VARCHAR(50) NOT NULL,
                        TableName NVARCHAR(100) NOT NULL,
                        CreateTime DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_{TABLE_CONFIG_NAME}_StationId ON {TABLE_CONFIG_NAME}(StationId);
                END";
            await DBCenter.UpdateAsync(sql);
        }

        public async Task<dynamic> GetTableListAsync(int stationId)
        {
            var station = await GetStationByIdAsync(stationId);
            if (station == null) throw new Exception("厂站不存在");

            var sourceDb = new SqlServerDBHelper(station.ConnectionString, Guid.NewGuid().ToString());
            // 获取所有表
            string allTablesSql = @"
                    SELECT 
                        t.name,
                        ep.value as description
                    FROM sys.tables t
                    LEFT JOIN sys.extended_properties ep 
                        ON ep.major_id = t.object_id 
                        AND ep.minor_id = 0 
                        AND ep.name = 'MS_Description'
                    ORDER BY t.name";

            var allTables = await sourceDb.FindAsync(allTablesSql);

            // 获取已配置的表
            string selectedTablesSql = $"SELECT TableName FROM {TABLE_CONFIG_NAME} WHERE StationId = @stationId";
            var selectedTables = await DBCenter.FindAsync(selectedTablesSql, new Parameter("@stationId", stationId));

            return new
            {
                allTables = allTables.Result,
                selectedTables = selectedTables.Result?.Select(t => t.Value<string>("TableName"))
            };
        }

        public async Task<dynamic> GetTableListAsync(string stationId)
        {
            var station = await GetStationByIdAsync(stationId);
            if (station == null) throw new Exception("厂站不存在");

            var sourceDb = new SqlServerDBHelper(station.ConnectionString, Guid.NewGuid().ToString());
            // 获取所有表
            string allTablesSql = @"
                SELECT 
                    t.name,
                    ep.value as description
                FROM sys.tables t
                LEFT JOIN sys.extended_properties ep 
                    ON ep.major_id = t.object_id 
                    AND ep.minor_id = 0 
                    AND ep.name = 'MS_Description'
                ORDER BY t.name";

            var allTables = await sourceDb.FindAsync(allTablesSql);

            // 获取已配置的表
            string selectedTablesSql = $"SELECT TableName FROM {TABLE_CONFIG_NAME} WHERE StationId = @stationId";
            var selectedTables = await DBCenter.FindAsync(selectedTablesSql, new Parameter("@stationId", stationId));

            return new
            {
                allTables = allTables.Result,
                selectedTables = selectedTables.Result?.Select(t => t.Value<string>("TableName"))
            };
        }

       
        public async Task SaveCollectConfigAsync(string stationId, List<string> tables)
        {
            await EnsureTableConfigExists();

            // 先删除原有配置
            string deleteSql = $"DELETE FROM {TABLE_CONFIG_NAME} WHERE StationId = @stationId";
            await DBCenter.DeleteAsync(deleteSql, new Parameter("@stationId", stationId));

            //根据站点ID获取站点信息
            var station = await GetStationByIdAsync(stationId);
            //定义原数据库帮助类
            IBaseDBHelper sourceDb = new SqlServerDBHelper(station.ConnectionString, stationId);
            if (tables?.Any() == true)
            {
                // 插入新配置
                foreach (var table in tables)
                {
                    string insertSql = $@"
                        INSERT INTO {TABLE_CONFIG_NAME} (StationId, TableName, CreateTime)
                        VALUES (@stationId, @tableName, GETDATE())";

                    await DBCenter.InsertAsync(insertSql,
                        new Parameter("@stationId", stationId),
                        new Parameter("@tableName", table));

                    //获取表的主键信息
                    string primaryKeySql = $@"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                        WHERE TABLE_NAME = @tableName AND CONSTRAINT_NAME LIKE '%PK%'";
                    var res = await sourceDb.FindOneAsync(primaryKeySql, new Parameter("@tableName", table));
                    var primaryKey = res.Result?.Value<string>("COLUMN_NAME");
                }
            }
        }

        public async Task BatchSaveCollectConfigAsync(List<string> stationIds, List<string> tables)
        {
            await EnsureTableConfigExists();

            foreach (var stationId in stationIds)
            {
                var station = await GetStationByIdAsync(stationId);
                if (station == null) continue;

                // 获取已存在的采集配置
                string existingSql = $"SELECT TableName FROM {TABLE_CONFIG_NAME} WHERE StationId = @stationId";
                var existingTables = await DBCenter.FindAsync(existingSql, new Parameter("@stationId", stationId));
                var existingTableNames = existingTables.Result?.Select(t => t.Value<string>("TableName")).ToList() ?? new List<string>();

                // 只添加不存在的表配置
                var newTables = tables.Where(t => !existingTableNames.Contains(t));

                foreach (var table in newTables)
                {
                    string insertSql = $@"
                        INSERT INTO {TABLE_CONFIG_NAME} (StationId, TableName, CreateTime)
                        VALUES (@stationId, @tableName, GETDATE())";

                    await DBCenter.InsertAsync(insertSql,
                        new Parameter("@stationId", stationId),
                        new Parameter("@tableName", table));
                }
            }
        }

       
        public void InitCTTimerTask()
        {
            //添加两个定时任务，一个采集全量，一个采集增量
            JobManager.AddJob(() =>
            {
                DoCT(0);
            }, s => {
                s.NonReentrant();
                s.ToRunNow().AndEvery(int.Parse(_cfg["CollectInterval"])).Seconds();
            });
            JobManager.AddJob(() =>
            {
                DoCT(1);
            }, s =>
            {
                s.NonReentrant();
                s.ToRunNow().AndEvery(int.Parse(_cfg["CollectInterval"])).Seconds();
            });

            //每小时调整全量表的状态为未完成，目的是避免全量表一直处于完成状态
            JobManager.AddJob(() =>
            {
                UpdateFullTableIsCompleted();
            }, s => {
                s.NonReentrant();
                s.ToRunEvery(1).Hours().At(0);
            });
        }
        private void DoCT(int isFull)
        {
            //判断证书是否有效，如果证书无效，则不执行采集任务
            //解析证书内容
            var licenseInfo = GetLicenseInfo();
            if (licenseInfo == null || licenseInfo.EndDate<DateTime.Now || licenseInfo.StartDate>DateTime.Now)
            {
                return; // 证书无效，直接返回
            }
            //获取所有站点
            var stations = GetStationList();
            //如果厂站个数大于证书限制的个数，则只取证书限制个数的厂站
            if (stations.Count() > licenseInfo.MaxStations)
            {
                stations = stations.Take(licenseInfo.MaxStations).ToList();
            }
            //循环遍历站点
            foreach (var station in stations)
            {
                // 获取已配置的表
                string selectedTablesSql = @$"SELECT a.TableName FROM {TABLE_CONFIG_NAME} a inner join
{FULL_SYNC_STATE_TABLE} b on a.TableName = b.TableName and a.StationId = b.SiteId WHERE StationId = @stationId and  b.IsCompleted = {isFull}";
                var selectedTables = DBCenter.Find(selectedTablesSql, new Parameter("@stationId", station.StationId));

                //拿到厂站的dbhelper
                var sourceDb = new SqlServerDBHelper(station.ConnectionString, station.StationId);

                //循环遍历表
                foreach (var table in selectedTables.Result)
                {
                    //获取表的主键信息
                    string primaryKeySql = $@"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                        WHERE TABLE_NAME = '{table["TableName"].ToString()}' AND CONSTRAINT_NAME LIKE '%PK%'";
                    var res = sourceDb.FindOne(primaryKeySql);
                    var primaryKey = res.Result?.Value<string>("COLUMN_NAME");
                    var ct = new ChangeTrackingDataCollector(sourceDb, DBCenter, table.Value<string>("TableName"), table.Value<string>("TableName"), primaryKey, station.StationId);
                    ct.CollectData();
                }
            }
        }

        private void UpdateFullTableIsCompleted()
        {
            string sql = $@"
                UPDATE {FULL_SYNC_STATE_TABLE} 
                SET IsCompleted = 0, UpdateTime = GETDATE()";
            DBCenter.UpdateAsync(sql);
        }

        private LicenseInfo GetLicenseInfo()
        {
            try
            {
                var licensePath = Path.Combine(_env.ContentRootPath, "license.lic");
                if (!System.IO.File.Exists(licensePath))
                {
                    return null;
                }

                var licenseContent = System.IO.File.ReadAllText(licensePath);

                var decryptedContent = RSAHelper.DecryptWithPrivateKey(licenseContent);
                var licenseInfo =  JObject.Parse(decryptedContent).ToObject<LicenseInfo>();
                return licenseInfo;
               
            }
            catch (Exception ex)
            {
                return  null;
            }
        }

        public async Task<dynamic> GetCollectorLogsAsync(string? stationId, string? tableName, string? logLevel, int page, int pageSize)
        {
            var conditions = new List<string>();
            var parameters = new List<Parameter>();

            if (!string.IsNullOrEmpty(stationId))
            {
                conditions.Add("l.SiteId = @siteId");
                parameters.Add(new Parameter("@siteId", stationId));
            }
            if (!string.IsNullOrEmpty(tableName))
            {
                conditions.Add("l.TableName = @tableName");
                parameters.Add(new Parameter("@tableName", tableName));
            }
            if (!string.IsNullOrEmpty(logLevel))
            {
                conditions.Add("l.LogLevel = @logLevel");
                parameters.Add(new Parameter("@logLevel", logLevel));
            }

            string whereClause = conditions.Any() ? $"WHERE {string.Join(" AND ", conditions)}" : "";
            
            // 获取总记录数
            string countSql = $@"
                SELECT COUNT(1) AS Total 
                FROM BZ_CT_CollectorLogs l
                LEFT JOIN {STATION_TABLE_NAME} s ON l.SiteId = s.StationId
                {whereClause}";
            var totalResult = await DBCenter.FindOneAsync(countSql, parameters.ToArray());
            int total = totalResult.Result["Total"].ToObject<int>();

            // 使用ROW_NUMBER()实现分页
            string sql = $@"
                WITH LogsCTE AS (
                    SELECT l.*, s.Name as StationName,
                           ROW_NUMBER() OVER (ORDER BY l.CreateTime DESC) as RowNum
                    FROM BZ_CT_CollectorLogs l
                    LEFT JOIN {STATION_TABLE_NAME} s ON l.SiteId = s.StationId
                    {whereClause}
                )
                SELECT * FROM LogsCTE 
                WHERE RowNum BETWEEN {(page - 1) * pageSize + 1} AND {page * pageSize}
                ORDER BY CreateTime DESC";

            var logs = await DBCenter.FindAsync(sql, parameters.ToArray());

            return new
            {
                total,
                rows = logs.Result
            };
        }
    }
}
