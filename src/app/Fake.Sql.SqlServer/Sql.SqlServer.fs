namespace Fake.Sql

/// Contains helpers around interacting with SQL Server databases.
[<RequireQualifiedAccess>]
module SqlServer =

    open System
    open System.IO
    open System.Data.SqlClient
    open Microsoft.SqlServer.Management.Smo
    open Microsoft.SqlServer.Management.Common

    open Fake.Core
    open Fake.IO
    open Fake.IO.FileSystemOperators

    type ServerInfo = {
        Server: Server
        ConnBuilder: SqlConnectionStringBuilder
    }

    let getServerInfo connectionString =
        let connBuilder = new SqlConnectionStringBuilder(connectionString)
        let conn = new ServerConnection()
        if not <| String.IsNullOrWhiteSpace(connBuilder.UserID) then
            conn.LoginSecure <- false
            conn.Login <- connBuilder.UserID

        if not <| String.IsNullOrWhiteSpace(connBuilder.Password) then
            conn.LoginSecure <- false
            conn.Password <- connBuilder.Password

        conn.ServerInstance <- connBuilder.DataSource
        conn.Connect()

        { Server = new Server(conn); ConnBuilder = connBuilder }

    /// Gets the `Database`s from the database server
    let getDatabasesFromServer (serverInfo:ServerInfo) =
        seq { for db in serverInfo.Server.Databases -> db }

    /// Gets the Database names from the database server
    let getDatabaseNamesFromServer (serverInfo:ServerInfo) =
        serverInfo
        |> getDatabasesFromServer
        |> Seq.map (fun db -> db.Name)

    let getInitialCatalog serverInfo = serverInfo.ConnBuilder.InitialCatalog

    /// Gets the name or network address of the instance of SQL Server
    let getServerName serverInfo = serverInfo.ConnBuilder.DataSource

    let dbExistsOnServer serverInfo dbName =
        let names = getDatabaseNamesFromServer serverInfo
        let searched = getInitialCatalog serverInfo
        Trace.tracefn "Searching for database %s on server %s. Found: " searched (getServerName serverInfo)
        names
        |> Seq.iter (Trace.tracefn " - %s ")

        names
        |> Seq.exists ((=) dbName)

    /// Gets the Initial Catalog as a `Database` instance
    let getDatabase serverInfo = new Database(serverInfo.Server, getInitialCatalog serverInfo)

    let initialCatalogExistsOnServer serverInfo =
        getInitialCatalog serverInfo
        |> dbExistsOnServer serverInfo

    let dropDb serverInfo =
        if initialCatalogExistsOnServer serverInfo then
            let initialCatalog = getInitialCatalog serverInfo
            Trace.logfn "Dropping database %s on server %s." initialCatalog (getServerName serverInfo)
            (getDatabase serverInfo).DropBackupHistory |> ignore
            initialCatalog |> serverInfo.Server.KillDatabase
        serverInfo

    let killAllProcesses serverInfo =
        let initialCatalog = getInitialCatalog serverInfo
        Trace.logfn "Killing all processes from database %s on server %s." initialCatalog (getServerName serverInfo)
        serverInfo.Server.KillAllProcesses initialCatalog
        serverInfo

    let detach serverInfo =
        serverInfo
        |> killAllProcesses
        |> fun si ->
                let initialCatalog = getInitialCatalog si
                Trace.logfn "Detaching database %s on server %s." initialCatalog (getServerName si)
                si.Server.DetachDatabase(initialCatalog, true)
                si

    let attach serverInfo (attachOptions:AttachOptions) files =
        let sc = new Collections.Specialized.StringCollection()
        files |> Seq.iter (fun file ->
            sc.Add file |> ignore
            File.checkExists file)

        let initialCatalog = getInitialCatalog serverInfo

        Trace.logfn "Attaching database %s on server %s." initialCatalog (getServerName serverInfo)
        serverInfo.Server.AttachDatabase(initialCatalog, sc, attachOptions)
        serverInfo

    let createDb serverInfo =
        Trace.logfn "Creating database %s on server %s." (getInitialCatalog serverInfo) (getServerName serverInfo)
        (getDatabase serverInfo).Create()
        serverInfo

    let runScript serverInfo sqlFile =
        Trace.logfn "Executing script %s." sqlFile
        sqlFile
        |> File.readAsString
        |> (getDatabase serverInfo).ExecuteNonQuery

    /// Closes the connection to the database server.
    let disconnect serverInfo =
        Trace.logfn "Disconnecting from server %s." (getServerName serverInfo)
        if isNull serverInfo.Server then
            failwith "Server is not configured."
        if isNull serverInfo.Server.ConnectionContext then
            failwith "Server.ConnectionContext is not configured."
        serverInfo.Server.ConnectionContext.Disconnect()

    let internal replaceDatabaseFilesF connectionString attachOptions copyF =
        connectionString
        |> getServerInfo
        |> fun si -> if dbExistsOnServer si (getInitialCatalog si) then detach si else si
        |> fun si -> copyF() |> attach si attachOptions
        |> disconnect

    /// Replaces the database files.
    let replaceDatabaseFiles connectionString targetDir files attachOptions =
        replaceDatabaseFilesF connectionString attachOptions
            (fun _ ->
                files
                |> Seq.map (fun fileName ->
                    let fi = new FileInfo(fileName)
                    Shell.copyFile targetDir fileName
                    targetDir @@ fi.Name))

    let replaceDatabaseFilesWithCache connectionString targetDir cacheDir files attachOptions =
        replaceDatabaseFilesF connectionString attachOptions
            (fun _ -> Shell.copyCached targetDir cacheDir files)

    let dropAndCreateDatabase connectionString =
        connectionString
        |> getServerInfo
        |> dropDb
        |> createDb
        |> disconnect

    let runScripts connectionString scripts =
        let serverInfo = getServerInfo connectionString
        scripts |> Seq.iter (runScript serverInfo)
        disconnect serverInfo

    let runScriptsFromDirectory connectionString scriptDirectory =
        System.IO.Directory.GetFiles(scriptDirectory, "*.sql")
        |> runScripts connectionString
