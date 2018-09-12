module Fake.Sql.SqlServer.IntegrationTests.Tests

open Expecto
open Expecto.Logging
open Expecto.Logging.Message
open System
open System.IO

open Fake.Core
open Fake.BuildServer
open Fake.Sql

let logger = Log.create "Sql Tests Log"

type AppveyorSqlLoginDetails = {
    Host : string
    UserName : string
    Password : string }
with member this.ConnectionString =
        sprintf "Data Source=%s; User=%s; Password=%s" this.Host this.UserName this.Password

let appveyorConnectionString =
    { Host = "localhost"
      UserName = "SA"
      Password = "Password12!" }.ConnectionString

let connectionString =
    if AppVeyor.detect() then appveyorConnectionString
    else "Data Source=.; Integrated Security=True"

let initialCatalogName = "TestDatabase"
let serverInfo = SqlServer.ServerInfo.create connectionString
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