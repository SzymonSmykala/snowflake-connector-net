﻿/*
 * Copyright (c) 2023 Snowflake Computing Inc. All rights reserved.
 */

namespace Snowflake.Data.Tests.UnitTests
{
    using NUnit.Framework;
    using Snowflake.Data.Configuration;
    using Snowflake.Data.Core;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    [TestFixture, NonParallelizable]
    class ChunkDownloaderFactoryTest
    {
        bool _useV2ChunkDownloaderDefault;
        int _chunkDownloaderVersionDefault;

        [SetUp]
        public void BeforeTest()
        {
            _useV2ChunkDownloaderDefault = SFConfiguration.Instance().UseV2ChunkDownloader;
            _chunkDownloaderVersionDefault = SFConfiguration.Instance().ChunkDownloaderVersion;
        }

        [TearDown]
        public void AfterTest()
        {
            SFConfiguration.Instance().UseV2ChunkDownloader = _useV2ChunkDownloaderDefault; // Return to default version
            SFConfiguration.Instance().ChunkDownloaderVersion = _chunkDownloaderVersionDefault; // Return to default version
        }

        private QueryExecResponseData mockQueryRequestData()
        {
            return new QueryExecResponseData
            {
                rowSet = new string[,] { { } },
                rowType = new List<ExecResponseRowType>(),
                parameters = new List<NameValueParameter>(),
                chunks = new List<ExecResponseChunk>{new ExecResponseChunk()
                {
                    url = "fake",
                    uncompressedSize = 100,
                    rowCount = 1
                }}
            };
        }

        private SFResultSet mockSFResultSet(QueryExecResponseData responseData, CancellationToken token)
        {
            string connectionString = "user=user;password=password;account=account;";
            SFSession session = new SFSession(connectionString, null);
            List<NameValueParameter> list = new List<NameValueParameter>
            {
                new NameValueParameter { name = "CLIENT_PREFETCH_THREADS", value = "3" }
            };
            session.UpdateSessionParameterMap(list);

            return new SFResultSet(responseData, new SFStatement(session), token);
        }

        [Test, NonParallelizable]
        public void TestGetDownloader([Values(false, true)] bool useV2ChunkDownloader, [Values(1, 2, 3, 4)] int chunkDownloaderVersion)
        {
            // Set configuration settings
            SFConfiguration.Instance().UseV2ChunkDownloader = useV2ChunkDownloader;
            SFConfiguration.Instance().ChunkDownloaderVersion = chunkDownloaderVersion;

            CancellationToken token = new CancellationToken();

            if (chunkDownloaderVersion == 4 && !useV2ChunkDownloader)
            {
                Exception ex = Assert.Throws<Exception>(() => ChunkDownloaderFactory.GetDownloader(null, null, token));
                Assert.AreEqual("Unsupported Chunk Downloader version specified in the SFConfiguration", ex.Message);
            }
            else
            {
                QueryExecResponseData responseData = mockQueryRequestData();
                SFResultSet resultSet = mockSFResultSet(responseData, token);

                IChunkDownloader downloader = ChunkDownloaderFactory.GetDownloader(responseData, resultSet, token);

                // GetDownloader() returns SFChunkDownloaderV2 when UseV2ChunkDownloader is true
                if (SFConfiguration.Instance().UseV2ChunkDownloader)
                {
                    Assert.IsTrue(downloader is SFChunkDownloaderV2);
                }
                else
                {
                    if (SFConfiguration.Instance().GetChunkDownloaderVersion() == 1)
                    {
                        Assert.IsTrue(downloader is SFBlockingChunkDownloader);
                    }
                    else if (SFConfiguration.Instance().GetChunkDownloaderVersion() == 2)
                    {
                        Assert.IsTrue(downloader is SFChunkDownloaderV2);
                    }
                    else if (SFConfiguration.Instance().GetChunkDownloaderVersion() == 3)
                    {
                        Assert.IsTrue(downloader is SFBlockingChunkDownloaderV3);
                    }
                }
            }
        }
    }
}
