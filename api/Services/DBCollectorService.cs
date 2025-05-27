using BaoZi.Tools.DBHelper;
using BaoZi.Tools.TimerTask;
using dbcollector_api.Models;
using FluentScheduler;
using Newtonsoft.Json.Linq;

namespace dbcollector_api.Services
{
    public class DBCollectorService
    {
        private readonly IBaseDBHelper DBCenter;
        private const string STATION_TABLE_NAME = "StationConfig";
        private const string TABLE_CONFIG_NAME = "CollectTableConfig";
        private const string FULL_SYNC_STATE_TABLE = "FullSyncState";
        private readonly TimerTaskService TimerTaskService;
        private readonly IConfiguration _cfg;
        public DBCollectorService(IBaseDBHelper baseDBHelper, TimerTaskService timerTaskService, IConfiguration cfg)
        {
            DBCenter = baseDBHelper;
            TimerTaskService = timerTaskService;
            _cfg = cfg;
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
        }
        private void DoCT(int isFull)
        {
            //获取所有站点
            var stations = GetStationList();
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
    }
}
