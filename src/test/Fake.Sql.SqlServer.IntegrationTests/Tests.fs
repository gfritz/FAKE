module Fake.Sql.SqlServer.IntegrationTests.Tests

open Expecto
open Expecto.Logging
open Expecto.Logging.Message
open System
open System.IO

open Fake.Core
open Fake.BuildServer
open Fake.Sql

type AppveyorSqlLoginDetails =
    { Host : string
      UserName : string
      Password : string }
with member this.ConnectionString =
        sprintf "Data Source=%s; User=%s; Password=%s" this.Host this.UserName this.Password

// https://www.appveyor.com/docs/getting-started-with-appveyor-for-linux/#sql-server-2017-for-linux
let appveyorConnectionString =
    { Host = "localhost"
      UserName = "SA"
      Password = "Password12!" }.ConnectionString

/// Returns the Sql Server connection string for the detected CI server
/// otherwise returns the provided `fallbackValue`.
let connectionStringOrDefault fallbackValue =
    if AppVeyor.detect() then appveyorConnectionString
    else fallbackValue

let initialCatalogName = "TestDatabase"

// how should someone pass in a default Data Source when testing locally?
// argument passed to target? environment variable?
let serverInfo = SqlServer.ServerInfo.create (connectionStringOrDefault @"Data Source=.; Integrated Security=True")
serverInfo.ConnBuilder.InitialCatalog <- initialCatalogName

[<Tests>]
let tests =
    testList "Fake.Sql.SqlServer.IntegrationTests tests" [
        yield test "connect to sqlserver" {
            Expect.isNotEmpty serverInfo.Server.Name "should have connected to sqlserver"
        }

        yield Fake.ContextHelper.fakeContextTestCase "create database" <| fun _ ->
            SqlServer.createDatabase serverInfo

            Expect.isTrue (SqlServer.databaseExistsOnServer serverInfo initialCatalogName) (sprintf "should have created %s." initialCatalogName)

        yield Fake.ContextHelper.fakeContextTestCase "delete database" <| fun _ ->
            SqlServer.dropDatabase serverInfo

            Expect.isFalse (SqlServer.databaseExistsOnServer serverInfo initialCatalogName) (sprintf "should have deleted %s." initialCatalogName)
    ]