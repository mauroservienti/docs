﻿using System;
using System.Linq;
using FastTests;
using Raven.Client.Documents;
using Raven.Tests.Core.Utils.Entities;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using Raven.Server.Utils;
using MongoDB.Driver;
using Raven.Client.Documents.Operations.TimeSeries;
using Raven.Client.Documents.Commands.Batches;
using PatchRequest = Raven.Client.Documents.Operations.PatchRequest;
using Raven.Client.Documents.Operations;
using Xunit.Abstractions;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Session.TimeSeries;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.BulkInsert;
using static Raven.Client.Documents.BulkInsert.BulkInsertOperation;
using Raven.Client.Documents.Indexes.TimeSeries;
using Raven.Client.Documents.Queries.TimeSeries;
using Raven.Client.Documents.Queries;

namespace SlowTests.Client.TimeSeries.Session
{
    public class TimeSeriesSessionTests
    {
        public TimeSeriesSessionTests(ITestOutputHelper output)
        {
        }

        public void SessionTests()
        {
            var store = new DocumentStore
            {
                Urls = new[] { "http://localhost:8080" },
                Database = "products"
            };
            store.Initialize();

            var baseline = DateTime.Today;

            // create time series
            #region timeseries_region_TimeSeriesFor_without_document_load
            // Open a session
            using (var session = store.OpenSession())
            {
                // Use the session to create a document
                session.Store(new { Name = "John" }, "users/john");

                // Create an instance of TimeSeriesFor
                // Pass an explicit document ID to the TimeSeriesFor constructor 
                // Append a HeartRate of 70 at the first-minute timestamp 
                session.TimeSeriesFor("users/john", "HeartRate")
                    .Append(baseline.AddMinutes(1), 70d, "watches/fitbit");

                session.SaveChanges();
            }
            #endregion

            // retrieve a single value
            #region timeseries_region_TimeSeriesFor_with_document_load
            using (var session = store.OpenSession())
            {
                // Use the session to load a document
                User user = session.Load<User>("users/john");

                // Pass the document object returned from session.Load as a param
                // Retrieve a single value from the time series
                IEnumerable<TimeSeriesEntry> val = session.TimeSeriesFor(user, "HeartRate")
                    .Get(DateTime.MinValue, DateTime.MaxValue);
            }
            #endregion

            // retrieve a single value - use the document object
            #region timeseries_region_TimeSeriesFor-Get-Single-Value-Using-Document-Object
            using (var session = store.OpenSession())
            {
                // Use the session to load a document
                User user = session.Load<User>("users/john");

                // Pass the document object returned from session.Load as a param
                // Retrieve a single value from the time series
                IEnumerable<TimeSeriesEntry> val = session.TimeSeriesFor(user, "HeartRate")
                    .Get(DateTime.MinValue, DateTime.MaxValue);
            }
            #endregion

            #region timeseries_region_Get-All-Entries-Using-Document-ID
            // retrieve all entries of a time-series named "HeartRate" 
            // by passing TimeSeriesFor.Get an explict document ID
            // Include all time points - from the first timestamp to the last
            TimeSeriesEntry[] val = session.TimeSeriesFor("users/john", "HeartRate")
                .Get(DateTime.MinValue, DateTime.MaxValue);
            #endregion

            #region timeseries_region_Pass-TimeSeriesFor-Get-Query-Results
            // Query for a document with the Name property "John" 
            // and get its HeartRate time-series values
            using (var session = store.OpenSession())
            {
                var baseline = DateTime.Today;

                IRavenQueryable<User> query = session.Query<User>()
                    .Where(u => u.Name == "John");

                var result = query.ToList();

                TimeSeriesEntry[] val = session.TimeSeriesFor(result[0], "HeartRate")
                    .Get(DateTime.MinValue, DateTime.MaxValue);

                session.SaveChanges();
            }
            #endregion

            #region timeseries_region_Get-NO-Named-Values
            // Is the stock's closing-price rising?
            bool goingUp = false;

            using (var session = store.OpenSession())
            {
                TimeSeriesEntry[] val = session.TimeSeriesFor("users/john", "StockPrices")
                    .Get();

                var closePriceDay1 = val[0].Values[1];
                var closePriceDay2 = val[1].Values[1];
                var closePriceDay3 = val[2].Values[1];

                if ((closePriceDay2 > closePriceDay1)
                    &&
                    (closePriceDay3 > closePriceDay2))
                    goingUp = true;
            }
            #endregion

            #region timeseries_region_Get-Named-Values
            // Is the stock's closing-price rising?
            bool goingUp = false;

            using (var session = store.OpenSession())
            {
                TimeSeriesEntry<StockPrice>[] val = session.TimeSeriesFor<StockPrice>("users/john")
                    .Get();

                var closePriceDay1 = val[0].Value.Close;
                var closePriceDay2 = val[1].Value.Close;
                var closePriceDay3 = val[2].Value.Close;

                if ((closePriceDay2 > closePriceDay1)
                    &&
                    (closePriceDay3 > closePriceDay2))
                    goingUp = true;
            }
            #endregion

            #region timeseries_region_Append-Named-Values-1
            using (var session = store.OpenSession())
            {
                session.Store(new User { Name = "John" }, "users/john");

                // Append coordinates
                session.TimeSeriesFor<RoutePoint>("users/john")
                    .Append(DateTime.Now, new RoutePoint
                    {
                        Latitude = 40.712776,
                        Longitude = -74.005974
                    }, "watches/anotherFirm");

                session.SaveChanges();
            }
            #endregion

            #region timeseries_region_Append-Named-Values-2
            // append multi-value entries by name
            using (var session = store.OpenSession())
            {
                session.Store(new User { Name = "John" }, "users/john");

                session.TimeSeriesFor<StockPrice>("users/john")
                .Append(baseline.AddDays(1), 
                    new StockPrice
                    {
                        Open = 52,
                        Close = 54,
                        High = 63.5,
                        Low = 51.4,
                        Volume = 9824,
                    }, "companies/kitchenAppliances");

                session.TimeSeriesFor<StockPrice>("users/john")
                .Append(baseline.AddDays(2), 
                    new StockPrice
                    {
                        Open = 54,
                        Close = 55,
                        High = 61.5,
                        Low = 49.4,
                        Volume = 8400,
                    }, "companies/kitchenAppliances");

                session.TimeSeriesFor<StockPrice>("users/john")
                .Append(baseline.AddDays(3), 
                    new StockPrice
                    {
                        Open = 55,
                        Close = 57,
                        High = 65.5,
                        Low = 50,
                        Volume = 9020,
                    }, "companies/kitchenAppliances");

                session.SaveChanges();
            }
            #endregion

            #region timeseries_region_Append-Unnamed-Values-2
            using (var session = store.OpenSession())
            {
                session.Store(new User { Name = "John" }, "users/john");

                session.TimeSeriesFor("users/john", "StockPrices")
                .Append(baseline.AddDays(1),
                    new[] { 52, 54, 63.5, 51.4, 9824 }, "companies/kitchenAppliances");

                session.TimeSeriesFor("users/john", "StockPrices")
                .Append(baseline.AddDays(2),
                    new[] { 54, 55, 61.5, 49.4, 8400 }, "companies/kitchenAppliances");

                session.TimeSeriesFor("users/john", "StockPrices")
                .Append(baseline.AddDays(3),
                    new[] { 55, 57, 65.5, 50, 9020 }, "companies/kitchenAppliances");

                session.SaveChanges();
            }
            #endregion

            double day1Volume;
            double day2Volume;
            double day3Volume;

            #region timeseries_region_Named-Values-Query
            // Named Values Query
            using (var session = store.OpenSession())
            {
                IRavenQueryable<TimeSeriesRawResult<StockPrice>> query =
                    session.Query<Company>()
                    .Where(c => c.Address1 == "New York")
                    .Select(q => RavenQuery.TimeSeries<StockPrice>(q, "StockPrices", baseline, baseline.AddDays(3))
                        .Where(ts => ts.Tag == "companies/kitchenAppliances")
                        .ToList());

                var result = query.ToList()[0];

                day1Volume = result.Results[0].Value.Volume;
                day2Volume = result.Results[1].Value.Volume;
                day3Volume = result.Results[2].Value.Volume;
            }
            #endregion

            #region timeseries_region_Unnamed-Values-Query
            using (var session = store.OpenSession())
            {
                IRavenQueryable<TimeSeriesRawResult> query =
                    session.Query<Company>()
                    .Where(c => c.Address1 == "New York")
                    .Select(q => RavenQuery.TimeSeries(q, "StockPrices", baseline, baseline.AddDays(3))
                        .Where(ts => ts.Tag == "companies/kitchenAppliances")
                        .ToList());

                var result = query.ToList()[0];

                day1Volume = result.Results[0].Values[4];
                day2Volume = result.Results[1].Values[4];
                day3Volume = result.Results[2].Values[4];
            }
            #endregion


            #region timeseries_region_Named-Values-Register
            store.TimeSeries.Register<User, RoutePoint>();
            #endregion 



            // retrieve time series names
            using (var session = store.OpenSession())
            {
                #region timeseries_region_Retrieve-TimeSeries-Names
                User user = session.Load<User>("users/john");
                List<string> tsNames = session.Advanced.GetTimeSeriesFor(user);
                #endregion
            }

            #region timeseries_region_TimeSeriesFor-Append-TimeSeries-Range
            var baseline = DateTime.Today;

            // Append 10 HeartRate values
            using (var session = store.OpenSession())
            {
                session.Store(new { Name = "John" }, "users/john");

                ISessionDocumentTimeSeries tsf = session.TimeSeriesFor("users/john", "HeartRate");

                for (int i = 0; i < 10; i++)
                {
                    tsf.Append(baseline.AddSeconds(i), new[] { 67d }, "watches/fitbit");
                }

                session.SaveChanges();
            }
            #endregion

            #region timeseries_region_Delete-TimeSeriesFor-Single-Time-Point
            var baseline = DateTime.Today;
            using (var session = store.OpenSession())
            {
                //Delete a single entry
                session.TimeSeriesFor("users/john", "HeartRate")
                    .Delete(baseline.AddMinutes(4));

                session.SaveChanges();
            }
            #endregion

            var baseline = DateTime.Today;

            // Append 10 HeartRate values
            using (var session = store.OpenSession())
            {
                session.Store(new { Name = "John" }, "users/john");

                var tsf = session.TimeSeriesFor("users/john", "HeartRate");

                for (int i = 0; i < 10; i++)
                {
                    tsf.Append(baseline.AddSeconds(i), new[] { 67d }, "watches/fitbit");
                }

                session.SaveChanges();
            }

            #region timeseries_region_TimeSeriesFor-Delete-Time-Points-Range
            // Delete a range of 4 values from the time series
            using (var session = store.OpenSession())
            {
                session.TimeSeriesFor("users/john", "HeartRate")
                    .Delete(baseline.AddSeconds(4), baseline.AddSeconds(7));

                session.SaveChanges();
            }
            #endregion

            #region timeseries_region_Append-With-IEnumerable
            using (var store = GetDocumentStore())
            {
                var baseline = DateTime.Today;

                // Open a session
                using (var session = store.OpenSession())
                {
                    // Create a document
                    session.Store(new { Name = "John" }, "users/john");

                    // Pass TimeSeriesFor an explicit document ID
                    // Append values at the first-minute timestamp 
                    session.TimeSeriesFor("users/john", "HeartRate")
                    .Append(baseline.AddMinutes(1),
                            new[] { 65d, 52d, 72d },
                            "watches/fitbit");

                    session.SaveChanges();
                }
            }
            #endregion


            #region timeseries_region_Load-Document-And-Include-TimeSeries
            // Load a document and Include a specified range of a time-series
            using (var session = store.OpenSession())
            {
                var baseline = DateTime.Today;

                User user = session.Load<User>("users/1-A", includeBuilder =>
                    includeBuilder.IncludeTimeSeries("HeartRate",
                    baseline.AddMinutes(200), baseline.AddMinutes(299)));

                IEnumerable<TimeSeriesEntry> val = session.TimeSeriesFor("users/1-A", "HeartRate")
                    .Get(baseline.AddMinutes(200), baseline.AddMinutes(299));
            }
            #endregion

            #region timeseries_region_Query-Document-And-Include-TimeSeries
            // Query for a document and include a whole time-series
            using (var session = store.OpenSession())
            {
                baseline = DateTime.Today;

                IRavenQueryable<User> query = session.Query<User>()
                    .Where(u => u.Name == "John")
                    .Include(includeBuilder => includeBuilder.IncludeTimeSeries(
                        "HeartRate", DateTime.MinValue, DateTime.MaxValue));

                var result = query.ToList();

                IEnumerable<TimeSeriesEntry> val = session.TimeSeriesFor(result[0], "HeartRate")
                    .Get(DateTime.MinValue, DateTime.MaxValue);
            }
            #endregion

            #region timeseries_region_Raw-Query-Document-And-Include-TimeSeries
            // Include a Time Series in a Raw Query
            using (var session = store.OpenSession())
            {
                baseline = DateTime.Today;

                var start = baseline;
                var end = baseline.AddHours(1);

                IRawDocumentQuery<User> query = session.Advanced.RawQuery<User>
                    ("from Users include timeseries('HeartRate', $start, $end)")
                        .AddParameter("start", start)
                        .AddParameter("end", end);

                var result = query.ToList();

                IEnumerable<TimeSeriesEntry> val = session.TimeSeriesFor(result[0], "HeartRate")
                    .Get(start, end);
            }
            #endregion

            #region TS_region-Session_Patch-Append-100-Random-TS-Entries
            var baseline = DateTime.Today;

            // Create arrays of timestamps and random values to patch
            List<double> values = new List<double>();
            List<DateTime> timeStamps = new List<DateTime>();

            for (var cnt = 0; cnt < 100; cnt++)
            {
                values.Add(68 + Math.Round(19 * new Random().NextDouble()));
                timeStamps.Add(baseline.AddSeconds(cnt));
            }

            session.Advanced.Defer(new PatchCommandData("users/1-A", null,
                new PatchRequest
                {
                    Script = @"
                                var i = 0;
                                for(i = 0; i < $values.length; i++)
                                {
                                    timeseries(id(this), $timeseries)
                                    .append (
                                       new Date($timeStamps[i]), 
                                       $values[i], 
                                       $tag);
                                }",

                    Values =
                    {
                                { "timeseries", "HeartRate" },
                                { "timeStamps", timeStamps },
                                { "values", values },
                                { "tag", "watches/fitbit" }
                    }
                }, null));

            session.SaveChanges();
            #endregion

            var baseline = DateTime.Today;

            // Create arrays of timestamps and random values to patch
            List<double> values = new List<double>();
            List<DateTime> timeStamps = new List<DateTime>();

            for (var cnt = 0; cnt < 100; cnt++)
            {
                values.Add(68 + Math.Round(19 * new Random().NextDouble()));
                timeStamps.Add(baseline.AddSeconds(cnt));
            }

            #region TS_region-Session_Patch-Append-100-TS-Entries
            session.Advanced.Defer(new PatchCommandData("users/1-A", null,
                new PatchRequest
                {
                    Script = @"
                                var i = 0;
                                for(i = 0; i < $values.length; i++)
                                {
                                    timeseries(id(this), $timeseries)
                                    .append (
                                       new Date($timeStamps[i]), 
                                       $values[i], 
                                       $tag);
                                }",

                    Values =
                    {
                                { "timeseries", "HeartRate" },
                                { "timeStamps", timeStamps },
                                { "values", values },
                                { "tag", "watches/fitbit" }
                    }
                }, null));

            session.SaveChanges();
            #endregion

            #region TS_region-Session_Patch-Delete-50-TS-Entries
            // Delete time-series entries
            session.Advanced.Defer(new PatchCommandData("users/1-A", null,
                new PatchRequest
                {
                    Script = @"timeseries(this, $timeseries)
                             .delete(
                                $from, 
                                $to
                              );",
                    Values =
                    {
                                { "timeseries", "HeartRate" },
                                { "from", baseline.AddSeconds(0) },
                                { "to", baseline.AddSeconds(49) }
                    }
                }, null));
            session.SaveChanges();
            #endregion

        }

        public void ReebUseTimeSeriesBatchOperation()
        {
            const string documentId = "users/john";

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User(), documentId);
                    session.SaveChanges();
                }

                #region timeseries_region_Append-Using-TimeSeriesBatchOperation
                var baseline = DateTime.Today;

                var timeSeriesExerciseHeartRate = new TimeSeriesOperation
                {
                    Name = "RoutineHeartRate"
                };

                timeSeriesExerciseHeartRate.Append(new TimeSeriesOperation.AppendOperation { Tag = "watches/fitbit", Timestamp = baseline.AddMinutes(1), Values = new[] { 79d } });
                timeSeriesExerciseHeartRate.Append(new TimeSeriesOperation.AppendOperation { Tag = "watches/fitbit", Timestamp = baseline.AddMinutes(2), Values = new[] { 82d } });
                timeSeriesExerciseHeartRate.Append(new TimeSeriesOperation.AppendOperation { Tag = "watches/fitbit", Timestamp = baseline.AddMinutes(3), Values = new[] { 80d } });
                timeSeriesExerciseHeartRate.Append(new TimeSeriesOperation.AppendOperation { Tag = "watches/fitbit", Timestamp = baseline.AddMinutes(4), Values = new[] { 78d } });

                var timeSeriesBatch = new TimeSeriesBatchOperation(documentId, timeSeriesExerciseHeartRate);

                store.Operations.Send(timeSeriesBatch);
                #endregion


                #region timeseries_region_Delete-Range-Using-TimeSeriesBatchOperation
                var deleteEntries = new TimeSeriesOperation
                {
                    Name = "RoutineHeartRate"
                };

                deleteEntries.Delete(new TimeSeriesOperation.DeleteOperation { From = baseline.AddMinutes(2), To = baseline.AddMinutes(3) });

                var deleteEntriesBatch = new TimeSeriesBatchOperation(documentId, deleteEntries);

                store.Operations.Send(deleteEntriesBatch);
                #endregion
            }
        }


        #region timeseries_region_Get-Single-Time-Series
        // Get all values of a single time-series
        TimeSeriesRangeResult singleTimeSeriesRange = store.Operations.Send(
                    new GetTimeSeriesOperation(documentId, "HeartRate", DateTime.MinValue, DateTime.MaxValue));
                #endregion

                #region timeseries_region_Get-Multiple-Time-Series
                // Get value ranges from two time-series using GetMultipleTimeSeriesOperation
                TimeSeriesDetails multipleTimesSeriesDetails = store.Operations.Send(
                        new GetMultipleTimeSeriesOperation(documentId, new List<TimeSeriesRange>
                            {
                                new TimeSeriesRange
                                {
                                    Name = "ExerciseHeartRate",
                                    From = baseTime.AddHours(1),
                                    To = baseTime.AddHours(10)
                                },

                                new TimeSeriesRange
                                {
                                    Name = "RestHeartRate",
                                    From = baseTime.AddHours(11),
                                    To = baseTime.AddHours(20)
                                }
                            }));
                #endregion

                #region timeseries_region_Use-BulkInsert-To-Append-2-Entries
                // Use BulkInsert to append 2 time-series entries
                using (BulkInsertOperation bulkInsert = store.BulkInsert())
                {
                    using (TimeSeriesBulkInsert timeSeriesBulkInsert = bulkInsert.TimeSeriesFor(documentID, "HeartRate"))
                    {
                        timeSeriesBulkInsert.Append(baseline.AddMinutes(2), 61d, "watches/fitbit");
                        timeSeriesBulkInsert.Append(baseline.AddMinutes(3), 62d, "watches/apple-watch");
                    }
                }
                #endregion

                #region timeseries_region_Use-BulkInsert-To-Append-100-Entries
                // Append 100 entries in a single transaction
                using (BulkInsertOperation bulkInsert = store.BulkInsert())
                {
                    using (TimeSeriesBulkInsert timeSeriesBulkInsert = bulkInsert.TimeSeriesFor(documentId, "HeartRate"))
                    {
                        for (int minute = 0; minute < 100; minute++)
                        {
                            timeSeriesBulkInsert.Append(baseline.AddMinutes(minute), new double[] { 80d }, "watches/fitbit");
                        }
                    }
                }
                #endregion

                #region BulkInsert-overload-2-Two-HeartRate-Sets
                ICollection<double> ExerciseHeartRate = new List<double>
                        { 89d, 82d, 85d };

                ICollection<double> RestingHeartRate = new List<double>
                        {59d, 63d, 61d, 64d, 64d, 65d };

                using (TimeSeriesBulkInsert timeSeriesBulkInsert = bulkInsert.TimeSeriesFor(documentId, "HeartRate"))
                {
                    timeSeriesBulkInsert.Append(baseline.AddMinutes(2), ExerciseHeartRate, "watches/fitbit");
                    timeSeriesBulkInsert.Append(baseline.AddMinutes(3), RestingHeartRate, "watches/apple-watch");
                }
                #endregion




                #region TS_region-Operation_Patch-Append-Single-TS-Entry
                store.Operations.Send(new PatchOperation("users/1-A", null,
                    new PatchRequest
                    {
                        Script = "timeseries(this, $timeseries).append($timestamp, $values, $tag);",
                        Values =
                        {
                            { "timeseries", "HeartRate" },
                            { "timestamp", baseline.AddMinutes(1) },
                            { "values", 59d },
                            { "tag", "watches/fitbit" }
                        }
                    }));
                #endregion

                #region TS_region-Operation_Patch-Append-100-TS-Entries
                var baseline = DateTime.Today;

                // Create arrays of timestamps and random values to patch
                List<double> values = new List<double>();
                List<DateTime> timeStamps = new List<DateTime>();

                for (var cnt = 0; cnt < 100; cnt++)
                {
                    values.Add(68 + Math.Round(19 * new Random().NextDouble()));
                    timeStamps.Add(baseline.AddSeconds(cnt));
                }

                store.Operations.Send(new PatchOperation("users/1-A", null,
                    new PatchRequest
                    {
                        Script = @"var i = 0; 
                            for (i = 0; i < $values.length; i++) 
                            {timeseries(id(this), $timeseries)
                            .append (
                                      new Date($timeStamps[i]), 
                                      $values[i], 
                                      $tag);
                            }",
                        Values =
                        {
                                { "timeseries", "HeartRate" },
                                { "timeStamps", timeStamps},
                                { "values", values },
                                { "tag", "watches/fitbit" }
                        }

                    }));
                #endregion

                #region TS_region-Operation_Patch-Delete-50-TS-Entries
                store.Operations.Send(new PatchOperation("users/1-A", null,
                    new PatchRequest
                    {
                        Script = "timeseries(this, $timeseries).delete($from, $to);",
                        Values =
                        {
                                { "timeseries", "HeartRate" },
                                { "from", baseline.AddSeconds(0) },
                                { "to", baseline.AddSeconds(49) }
                        }
                    }));
                #endregion

                #region TS_region-PatchByQueryOperation-Append-To-Multiple-Docs
                // Append time-series to all users
                PatchByQueryOperation appendOperation = new PatchByQueryOperation(new IndexQuery
                {
                    Query = @"from Users as u update
                                {
                                    timeseries(u, $name).append($time, $values, $tag)
                                }",
                    QueryParameters = new Parameters
                            {
                                { "name", "HeartRate" },
                                { "time", baseline.AddMinutes(1) },
                                { "values", new[]{59d} },
                                { "tag", "watches/fitbit" }
                            }
                });
                store.Operations.Send(appendOperation);
                #endregion

                #region TS_region-PatchByQueryOperation-Delete-From-Multiple-Docs
                // Delete time-series from all users
                PatchByQueryOperation deleteOperation = new PatchByQueryOperation(new IndexQuery
                {
                    Query = @"from Users as u
                                update
                                {
                                    timeseries(u, $name).delete($from, $to)
                                }",
                    QueryParameters = new Parameters
                            {
                                { "name", "HeartRate" },
                                { "from", DateTime.MinValue },
                                { "to", DateTime.MaxValue }
                            }
                });
                store.Operations.Send(deleteOperation);
                #endregion

                #region TS_region-PatchByQueryOperation-Get
                PatchByQueryOperation getExerciseHeartRateOperation = new PatchByQueryOperation(new IndexQuery
                {
                    Query = @"
                        declare function foo(doc){
                            var entries = timeseries(doc, $name).get($from, $to);
                            var differentTags = [];
                            for (var i = 0; i < entries.length; i++)
                            {
                                var e = entries[i];
                                if (e.Tag !== null)
                                {
                                    if (!differentTags.includes(e.Tag))
                                    {
                                        differentTags.push(e.Tag);
                                    }
                                }
                            }
                            doc.NumberOfUniqueTagsInTS = differentTags.length;
                            return doc;
                        }

                        from Users as u
                        update
                        {
                            put(id(u), foo(u))
                        }",

                    QueryParameters = new Parameters
                    {
                        { "name", "ExerciseHeartRate" },
                        { "from", DateTime.MinValue },
                        { "to", DateTime.MaxValue }
                    }
                });

                var result = store.Operations.Send(getExerciseHeartRateOperation).WaitForCompletion();

                #endregion

                #region ts_region_Raw-Query-Non-Aggregated-Declare-Syntax
                // May 17 2020, 00:00:00
                var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                // Raw query with no aggregation - Declare syntax
                IRawDocumentQuery<TimeSeriesRawResult> nonAggregatedRawQuery =
                    session.Advanced.RawQuery<TimeSeriesRawResult>(@"
                            declare timeseries getHeartRate(user) 
                            {
                                from user.HeartRate 
                                    between $start and $end
                                    offset '02:00'
                            }
                            from Users as u where Age < 30
                            select getHeartRate(u)
                            ")
                    .AddParameter("start", baseline)
                    .AddParameter("end", baseline.AddHours(24));

                var nonAggregatedRawQueryResult = nonAggregatedRawQuery.ToList();
                #endregion

                #region ts_region_Raw-Query-Non-Aggregated-Select-Syntax
                // May 17 2020, 00:00:00
                var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                // Raw query with no aggregation - Select syntax
                IRawDocumentQuery<TimeSeriesRawResult> nonAggregatedRawQuery =
                    session.Advanced.RawQuery<TimeSeriesRawResult>(@"
                            from Users as u where Age < 30                            
                            select timeseries (
                                from HeartRate 
                                    between $start and $end
                                    offset '02:00'
                            )")
                    .AddParameter("start", baseline)
                    .AddParameter("end", baseline.AddHours(24));

                var nonAggregatedRawQueryResult = nonAggregatedRawQuery.ToList();
                #endregion

                #region ts_region_Raw-Query-Aggregated
                // May 17 2020, 00:00:00
                var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                // Raw Query with aggregation
                IRawDocumentQuery<TimeSeriesAggregationResult> aggregatedRawQuery =
                    session.Advanced.RawQuery<TimeSeriesAggregationResult>(@"
                            from Users as u
                            select timeseries(
                                from HeartRate 
                                    between $start and $end
                                group by '1 days'
                                select min(), max())
                            ")
                    .AddParameter("start", baseline)
                    .AddParameter("end", baseline.AddDays(7));

                var aggregatedRawQueryResult = aggregatedRawQuery.ToList();
                #endregion

                #region ts_region_LINQ-1-Select-Timeseries
                using (var session = store.OpenSession())
                {
                    IRavenQueryable<TimeSeriesRawResult> query = (IRavenQueryable<TimeSeriesRawResult>)session
                        .Query<User>()
                            .Where(u => u.Age < 30)
                                .Select(q => RavenQuery.TimeSeries(q, "HeartRate")
                                .ToList());

                    var result = query.ToList();
                }
                #endregion

                #region ts_region_LINQ-2-RQL-Equivalent
                using (var session = store.OpenSession())
                {
                    IRawDocumentQuery<TimeSeriesRawResult> nonAggregatedRawQuery =
                        session.Advanced.RawQuery<TimeSeriesRawResult>(@"
                            from Users as u where Age < 30                            
                            select timeseries (
                                from HeartRate
                            )");

                    var nonAggregatedRawQueryResult = nonAggregatedRawQuery.ToList();
                }
                #endregion

                // Query - LINQ format with Range selection
                using (var session = store.OpenSession())
                {
                    var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                    #region ts_region_LINQ-3-Range-Selection
                    IRavenQueryable<TimeSeriesRawResult> query =
                        (IRavenQueryable<TimeSeriesRawResult>)session.Query<User>()
                            .Where(u => u.Age < 30)
                            .Select(q => RavenQuery.TimeSeries(q, "HeartRate", baseline, baseline.AddDays(3))
                            .ToList());
                    #endregion

                    var result = query.ToList();
                }

                using (var session = store.OpenSession())
                {
                    var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                    #region ts_region_LINQ-4-Where
                    IRavenQueryable<TimeSeriesRawResult> query =
                    (IRavenQueryable<TimeSeriesRawResult>)session.Query<User>()

                            // Choose user profiles of users under the age of 30
                            .Where(u => u.Age < 30)

                            .Select(q => RavenQuery.TimeSeries(q, "HeartRate", baseline, baseline.AddDays(3))

                            // Filter time-series entries by their tag.  
                            .Where(ts => ts.== "watches/fitbit")
                    #endregion

                            .ToList());

                    var result = query.ToList();
                }

                using (var session = store.OpenSession())
                {
                    // May 17 2020, 00:00:00
                    var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                    #region ts_region_Filter-By-load-Tag-Raw-RQL
                    IRawDocumentQuery<TimeSeriesRawResult> nonAggregatedRawQuery =
                        session.Advanced.RawQuery<TimeSeriesRawResult>(@"
                            from Companies as c where c.Address.Country = 'USA'
                            select timeseries(
                                from StockPrice
                                   load Tag as emp
                                   where emp.Title == 'Sales Representative'
                            )");

                    var result = nonAggregatedRawQuery.ToList();
                    #endregion
                }
            }

            // Query - LINQ format - LoadByTag to find a stock broker
            using (var session = store.OpenSession())
            {
                var baseline = new DateTime(2020, 5, 17, 00, 00, 00);

                #region ts_region_Filter-By-LoadByTag-LINQ
                IRavenQueryable<TimeSeriesRawResult> query =
                    (IRavenQueryable<TimeSeriesRawResult>)session.Query<Orders.Company>()

                        .Where(c => c.Address.Country == "USA")
                        .Select(q => RavenQuery.TimeSeries(q, "StockPrice")
                        
                        .LoadByTag<Employee>()
                        .Where((ts, src) => src.Title == "Sales Representative")
                        
                        .ToList());

                var result = query.ToList();
                #endregion
            }


            // Query - LINQ format - Aggregation 
            using (var session = store.OpenSession())
                {
                    var baseline = DateTime.Today;

                    #region ts_region_LINQ-6-Aggregation
                    IRavenQueryable<TimeSeriesAggregationResult> query = session.Query<User>()
                        .Where(u => u.Age > 72)
                        .Select(q => RavenQuery.TimeSeries(q, "HeartRate", baseline, baseline.AddDays(10))
                            .Where(ts => ts.Tag == "watches/fitbit")
                            .GroupBy(g => g.Days(1))
                            .Select(g => new
                            {
                                Avg = g.Average(),
                                Cnt = g.Count()
                            })
                            .ToList());
                    #endregion


                // Query - LINQ format - StockPrice
                using (var session = store.OpenSession())
                {
                    var baseline = new DateTime(2020, 5, 17);

                    #region ts_region_LINQ-Aggregation-and-Projections-StockPrice
                    IRavenQueryable<TimeSeriesAggregationResult> query = session.Query<Orders.Company>()
                        .Where(c => c.Address.Country == "USA")
                        .Select(q => RavenQuery.TimeSeries(q, "StockPrice")
                            .Where(ts => ts.Values[4] > 500000)
                            .GroupBy(g => g.Days(7))
                            .Select(g => new
                            {
                                Min = g.Min(),
                                Max = g.Max()
                            })
                            .ToList());

                    var result = query.ToList();
                    #endregion
                }

                using (var session = store.OpenSession())
                {
                    var baseline = new DateTime(2020, 5, 17);

                    var start = baseline;
                    var end = baseline.AddHours(1);

                    #region ts_region_Raw-RQL-Select-Syntax-Aggregation-and-Projections-StockPrice
                    IRawDocumentQuery<TimeSeriesAggregationResult> aggregatedRawQuery =
                        session.Advanced.RawQuery<TimeSeriesAggregationResult>(@"
                            from Companies as c
                                where c.Address.Country = 'USA'
                                select timeseries ( 
                                    from StockPrice 
                                    where Values[4] > 500000
                                        group by '7 day'
                                        select max(), min()
                                )
                            ");

                    var aggregatedRawQueryResult = aggregatedRawQuery.ToList();
                    #endregion
                }

                // Raw Query - StockPrice
                using (var session = store.OpenSession())
                {
                    var baseline = new DateTime(2020, 5, 17);

                    var start = baseline;
                    var end = baseline.AddHours(1);

                    #region ts_region_Raw-RQL-Declare-Syntax-Aggregation-and-Projections-StockPrice
                    IRawDocumentQuery<TimeSeriesAggregationResult> aggregatedRawQuery =
                        session.Advanced.RawQuery<TimeSeriesAggregationResult>(@"
                            declare timeseries SP(c) {
                                from c.StockPrice
                                where Values[4] > 500000
                                group by '7 day'
                                select max(), min()
                            }
                            from Companies as c
                            where c.Address.Country = 'USA'
                            select c.Name, SP(c)"
                            );

                    var aggregatedRawQueryResult = aggregatedRawQuery.ToList();
                    #endregion
                }


                var result = query.ToList();
                }

                // index queries
                #region ts_region_Index-TS-Queries-1-session-Query
                // Query time-series index using session.Query
                using (var session = store.OpenSession())
                {
                    List<SimpleIndex.Result> results = session.Query<SimpleIndex.Result, SimpleIndex>()
                        .ToList();
                }
                #endregion

                #region ts_region_Index-TS-Queries-2-session-Query-with-Linq
                // Enhance the query using LINQ expressions
                var chosenDate = new DateTime(2020, 5, 20);
                using (var session = store.OpenSession())
                {
                    List<SimpleIndex.Result> results = session.Query<SimpleIndex.Result, SimpleIndex>()
                        .Where(w => w.Date < chosenDate)
                        .OrderBy(o => o.HeartBeat)
                        .ToList();
                }
                #endregion

                #region ts_region_Index-TS-Queries-3-DocumentQuery
                // Query time-series index using DocumentQuery
                using (var session = store.OpenSession())
                {
                    List<SimpleIndex.Result> results = session.Advanced.DocumentQuery<SimpleIndex.Result, SimpleIndex>()
                        .ToList();
                }
                #endregion

                #region ts_region_Index-TS-Queries-4-DocumentQuery-with-Linq
                // Query time-series index using DocumentQuery with Linq-like expressions
                using (var session = store.OpenSession())
                {
                    List<SimpleIndex.Result> results = session.Advanced.DocumentQuery<SimpleIndex.Result, SimpleIndex>()
                        .WhereEquals("Tag", "watches/fitbit")
                        .ToList();
                }
                #endregion

                #region ts_region_Index-TS-Queries-5-session-Query-Async
                // Time-series async index query using session.Query
                using (var session = store.OpenAsyncSession())
                {
                    List<SimpleIndex.Result> results = await session.Query<SimpleIndex.Result, SimpleIndex>()
                        .ToListAsync();
                }
                #endregion


            }
        }

        #region ts_region_Index-TS-Queries-6-Index-Definition-And-Results-Class
        public class SimpleIndex : AbstractTimeSeriesIndexCreationTask<Employee>
        {

            public class Result
            {
                public double HeartBeat { get; set; }
                public DateTime Date { get; set; }
                public string User { get; set; }
                public string Tag { get; set; }
            }

            public SimpleIndex()
            {
                AddMap(
                    "HeartRate",
                    timeSeries => from ts in timeSeries
                                  from entry in ts.Entries
                                  select new
                                  {
                                      HeartBeat = entry.Values[0],
                                      entry.Timestamp.Date,
                                      User = ts.DocumentId,
                                      Tag = entry.Tag
                                  });
            }
        }
        #endregion



        private IDisposable GetDocumentStore()
        {
            throw new NotImplementedException();
        }
    }

    internal class User
    {
    }

    private interface IFoo
    {
        #region TimeSeriesFor-Append-definition-double
        // Append an entry with a single value (double)
        void Append(DateTime timestamp, double value, string tag = null);
        #endregion

        #region TimeSeriesFor-Append-definition-inum
        // Append an entry with multiple values (IEnumerable)
        void Append(DateTime timestamp, IEnumerable<double> values, string tag = null);
        #endregion

        #region TimeSeriesFor-Delete-definition-single-timepoint
        // Delete a single time-series entry
        void Delete(DateTime at);
        #endregion

        #region TimeSeriesFor-Delete-definition-range-of-timepoints
        // Delete a range of time-series entries
        void Delete(DateTime from, DateTime to);
        #endregion

        #region TimeSeriesFor-Get-definition
        TimeSeriesEntry[] Get(DateTime? from = null, DateTime? to = null, 
            int start = 0, int pageSize = int.MaxValue);
        #endregion

        #region TimeSeriesFor-Get-Named-Values
        //The stongly-typed API is used, to address time series values by name.
        TimeSeriesEntry<TValues>[] Get(DateTime? from = null, DateTime? to = null, 
            int start = 0, int pageSize = int.MaxValue);
        #endregion

        #region IncludeTimeSeries-definition
        TBuilder IncludeTimeSeries(string name, DateTime from, DateTime to);
        #endregion

        #region BulkInsert.TimeSeriesFor-definition
        public TimeSeriesBulkInsert TimeSeriesFor(string id, string name)
        #endregion


        #region GetTimeSeriesFor-definition
        List<string> GetTimeSeriesFor<T>(T instance);
        #endregion

        #region Load-definition
        T Load<T>(string id, Action<IIncludeBuilder<T>> includes);
        #endregion

        #region Include-definition
        IRavenQueryable<TResult> Include<TResult>(this IQueryable<TResult> source, 
            Action<IQueryIncludeBuilder<TResult>> includes)
        #endregion

        #region RawQuery-definition
        IRawDocumentQuery<T> RawQuery<T>(string query);
        #endregion

        #region Query-definition
        IRavenQueryable<T> Query<T>(string indexName = null, 
            string collectionName = null, bool isMapReduce = false);
        #endregion

        #region PatchCommandData-definition
        public PatchCommandData(string id, string changeVector, PatchRequest patch, 
            PatchRequest patchIfMissing)
        #endregion

        #region PatchRequest-definition
        public class PatchRequest
        {
            // Patching script
            public string Script { get; set; }
            // Values that can be used by the patching script
            public Dictionary<string, object> Values { get; set; }
            //...
        }
        #endregion

        #region TimeSeriesBatchOperation-definition
        public TimeSeriesBatchOperation(string documentId, TimeSeriesOperation operation)
        #endregion

        #region Append-Operation-Definition-1
        // Each appended entry has a single value.
        public void Append(DateTime timestamp, double value, string tag = null)
        #endregion

        #region Append-Operation-Definition-2
        // Each appended entry has multiple values.
        public void Append(DateTime timestamp, ICollection<double> values, string tag = null)
        #endregion

        #region AppendOperation-class
        public class AppendOperation
            {
                public DateTime Timestamp;
                public double[] Values;
                public string Tag;
                //...
            }
        #endregion

        #region DeleteOperation-class
        public class DeleteOperation
        {
            public DateTime From, To;
            //...
        }
        #endregion

        #region TimeSeriesRangeResult-class
        public class TimeSeriesRangeResult
        {
            public DateTime From, To;
            public TimeSeriesEntry[] Entries;
            public long? TotalResults;
            
            //..
        }
        #endregion

        #region GetTimeSeriesOperation-Definition
        public GetTimeSeriesOperation(string docId, string timeseries, 
            DateTime? @from = null, DateTime? to = null, int start = 0, int pageSize = int.MaxValue)
        #endregion

        #region TimeSeriesDetails-class
        public class TimeSeriesDetails
        {
            public string Id { get; set; }
            public Dictionary<string, List<TimeSeriesRangeResult>> Values { get; set; }
        }
        #endregion

        #region GetMultipleTimeSeriesOperation-Definition
        public GetMultipleTimeSeriesOperation(string docId, IEnumerable<TimeSeriesRange> ranges, 
            int start = 0, int pageSize = int.MaxValue)
        #endregion

        #region TimeSeriesRange-class
        public class TimeSeriesRange
        {
            public string Name;
            public DateTime From, To;
        }
        #endregion

        #region PatchOperation-Definition
        public PatchOperation(string id, string changeVector, PatchRequest patch, 
            PatchRequest patchIfMissing = null, bool skipPatchIfChangeVectorMismatch = false)
        #endregion

        #region PatchByQueryOperation-Definition
        public PatchByQueryOperation(IndexQuery queryToUpdate, QueryOperationOptions options = null)
        #endregion

        #region Store-Operations-send-Definition
        public Operation Send(IOperation<OperationIdResult> operation, SessionInfo sessionInfo = null)
        #endregion

        #region RavenQuery-TimeSeries-Definition-With-Range
        public static ITimeSeriesQueryable TimeSeries(object documentInstance, 
            string name, DateTime from, DateTime to)
        #endregion

        #region RavenQuery-TimeSeries-Definition-Without-Range
        public static ITimeSeriesQueryable TimeSeries(object documentInstance, string name)
        #endregion


    #region Register-Definitions
    public void Register<TCollection, TTimeSeriesEntry>(string name = null)
    public void Register<TCollection>(string name, string[] valueNames)
    public void Register(string collection, string name, string[] valueNames)
    #endregion




    #region TimeSeriesEntry-Definition
    public class TimeSeriesEntry
    {
        public DateTime Timestamp { get; set; }
        public double[] Values { get; set; }
        public string Tag { get; set; }
        public bool IsRollup { get; set; }

        public double Value

        //..
    }
    #endregion

    #region Custom-Data-Type-1
    private struct StockPrice
    {
        [TimeSeriesValue(0)] public double Open;
        [TimeSeriesValue(1)] public double Close;
        [TimeSeriesValue(2)] public double High;
        [TimeSeriesValue(3)] public double Low;
        [TimeSeriesValue(4)] public double Volume;
    }
    #endregion

    #region Custom-Data-Type-2
    private struct RoutePoint
    {
        [TimeSeriesValue(0)] public double Latitude;
        [TimeSeriesValue(1)] public double Longitude;
    }
    #endregion


}


}
