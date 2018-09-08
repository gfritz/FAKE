module Fake.Sql.SqlServer.IntegrationTests.Tests

open Expecto
open Expecto.Logging
open Expecto.Logging.Message
open System
open System.IO

open Fake.Core
open Fake.Sql

let logger = Log.create "Sql Tests Log"

[<Tests>]
let tests =
    testList "Fake.Sql.SqlServer.IntegrationTests tests" [
        yield test "connect to sqlserver" {
            let serverInfo = SqlServer.ServerInfo.create ""
            logger.info ( eventX serverInfo.Server.Name )
            Expect.isNotEmpty serverInfo.Server.Name "should have connected to sqlserver"
        }
    ]