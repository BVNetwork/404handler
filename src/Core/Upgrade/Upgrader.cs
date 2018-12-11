﻿using BVNetwork.NotFound.Core.Data;

using EPiServer.Logging;

namespace BVNetwork.NotFound.Core.Upgrade
{
    public static class Upgrader
    {
        private static readonly ILogger Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static bool Valid { get; set; }

        public static void Start(int version)
        {
            if (version == -1)
            {
                Create();
            }
            else
            {
                Upgrade();
            }
        }
        /// <summary>
        /// Create redirects and suggestions tables and SP for version number
        /// </summary>
        private static void Create()
        {
            var dba = DataAccessBaseEx.GetWorker();

            var created = CreateSuggestionsTable(dba);

            if (created)
            {
                created = CreateVersionNumberSp(dba);
            }

            Valid = created;
        }

        private static bool CreateSuggestionsTable(DataAccessBaseEx dba)
        {
            Log.Information("Create 404 handler suggestions table START");
            var createTableScript = @"CREATE TABLE [dbo].[BVN.NotFoundRequests](
                                        [ID] [int] IDENTITY(1,1) NOT NULL,
                                        [OldUrl] [nvarchar](2000) NOT NULL,
                                        [Requested] [datetime] NULL,
                                        [Referer] [nvarchar](2000) NULL
                                        ) ON [PRIMARY]";
            var created = dba.ExecuteNonQuery(createTableScript);
            Log.Information("Create 404 handler suggestions table END");

            if (created)
            {
                created = CreateSuggestionsTableIndex(dba);
            }

            return created;
        }

        private static bool CreateSuggestionsTableIndex(DataAccessBaseEx dba)
        {
            Log.Information("Create suggestions table clustered index START");
            var clusteredIndex =
                "CREATE CLUSTERED INDEX NotFoundRequests_ID ON [dbo].[BVN.NotFoundRequests] (ID)";

            var created = dba.ExecuteNonQuery(clusteredIndex);
            if (!created)
            {
                created = false;
                Log.Error("An error occurred during the creation of the 404 handler redirects clustered index. Canceling.");
            }

            Log.Information("Create suggestions table clustered index END");
            return created;
        }

        private static bool CreateVersionNumberSp(DataAccessBaseEx dba)
        {
            Log.Information("Create 404 handler version SP START");
            var versionSp =
                $@"CREATE PROCEDURE [dbo].[bvn_notfoundversion] AS RETURN {Configuration.Configuration.CurrentVersion}";

            var created = dba.ExecuteNonQuery(versionSp);

            if (!created)
            {
                Log.Error("An error occured during the creation of the 404 handler version stored procedure. Canceling.");
            }

            Log.Information("Create 404 handler version SP END");
            return created;
        }

        private static void Upgrade()
        {
            var dba = DataAccessBaseEx.GetWorker();

            var indexCheck =
                "SELECT COUNT(*) FROM sys.indexes WHERE name='NotFoundRequests_ID' AND object_id = OBJECT_ID('[dbo].[BVN.NotFoundRequests]')";

            var num = dba.ExecuteScalar(indexCheck);
            if (num == 0)
            {
                if (!dba.ExecuteNonQuery("CREATE CLUSTERED INDEX NotFoundRequests_ID ON [dbo].[BVN.NotFoundRequests] (ID)"))
                {
                    Valid = false;
                    Log.Error("An error occurred during the creation of the 404 handler redirects clustered index. Canceling.");
                }
                Log.Information("Create Clustered index END");
            }
            if (Valid)
            {
                var versionSp = @"ALTER PROCEDURE [dbo].[bvn_notfoundversion] AS RETURN " + Configuration.Configuration.CurrentVersion;
                Valid = dba.ExecuteNonQuery(versionSp);
            }
        }
    }
}