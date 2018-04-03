module Api.App

open System
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Orleankka
open Orleankka.Client
open Orleankka.Cluster
open Orleankka.FSharp

open Api.HttpHandlers
open Api.Notifications

// Opened after Giraffe to shadow its `TaskBuilder` implementation.
open FSharp.Control.Tasks

let getStartedHost () = async {
    let sb = Orleans.Hosting.SiloHostBuilder()
    sb.AddAssembly(Assembly.GetExecutingAssembly())
    sb.ConfigureOrleankka() |> ignore

    return! sb.Start() |> Async.AwaitTask
}

let getActorSystem (host: Orleans.Hosting.ISiloHost) = async {
    let! client = host.Connect() |> Async.AwaitTask
    return client.ActorSystem()
}

let sendNotification actorSystem messageId (message: Api.Models.Message) = task {
    let queue = ActorSystem.typedActorOf<INotificationQueue, NotificationMessage>(actorSystem, message.clientId)
    do! queue <! Send message.text
    return Ok ()
}

let webApp actorSystem =
    choose [
        subRoute "/api"
            (choose [
                POST >=> choose [
                    route "/send-message" >=> handleSendMessage (sendNotification actorSystem)
                ]
            ])
        setStatusCode 404 >=> text "Not Found" ]

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp actorSystem (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseCors(configureCors)
        .UseGiraffe(webApp actorSystem)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

let startWebServer actorSystem =
    WebHostBuilder()
        .UseKestrel()
        .UseIISIntegration()
        .Configure(Action<IApplicationBuilder>(configureApp actorSystem))
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .RunAsync()

let sscanf (pf:PrintfFormat<_,_,_,_,'t>) s : 't =
    let formatStr = pf.Value
    let constants = formatStr.Split([|"%s"|], StringSplitOptions.None)
    let regex = Regex("^" + String.Join("(.*?)", constants |> Array.map Regex.Escape) + "$")
    let matches =
        regex.Match(s).Groups
        |> Seq.cast<Group>
        |> Seq.skip 1
        |> Seq.map (fun g -> g.Value |> box)
    FSharp.Reflection.FSharpValue.MakeTuple(matches |> Seq.toArray, typeof<'t>) :?> 't

let getQueueSubscription actorSystem clientId : StreamRef<string>=
    ActorSystem.streamOf (actorSystem, "sms", clientId)

let handleUserInput actorSystem = async {
    printfn """
Usage:
    subscribe <clientId>    - add client notification subscription. If already existing, old will be unsubscribed.
    unsubscribe <clientId>  - remove client notification subscription.
"""

    let rec loop (clients: Map<string, StreamSubscription>) = async {
        match sscanf "%s %s" (Console.ReadLine()) with
        | "subscribe", clientId ->
            match Map.tryFind clientId clients with
            | Some subscription ->
                printfn "Removing existing client subscription."
                do! subscription.Unsubscribe() |> Async.AwaitTask
            | None -> ()

            let! subscription = async {
                let subRef = getQueueSubscription actorSystem clientId
                return!
                    subRef.Subscribe(printfn "Client %s received a message: %s" clientId)
                    |> Async.AwaitTask
            }
            printfn "Client %s has been subscribed" clientId
            return! loop <| Map.add clientId subscription clients

        | "unsubscribe", clientId ->
            match Map.tryFind clientId clients with
            | Some subscription ->
                do! subscription.Unsubscribe() |> Async.AwaitTask
            | None -> ()

            printfn "Client %s has been unsubscribed" clientId
            return! loop <| Map.remove clientId clients

        | unrecognized ->
            printfn "Unrecognized command"
            return! loop clients
    }

    return! loop Map.empty
}

[<EntryPoint>]
let main _ =

    let app = async {
        printfn "Starting Orleans silo, this may take several seconds..."

        let! actorSystem = async {
            let! host = getStartedHost ()
            return! getActorSystem host
        }

        printfn "Orleans silo started."
        printfn "Starting web API server..."

        let! serverComplete = startWebServer actorSystem |> Async.AwaitTask |> Async.StartChild

        printfn "Starting interactive client loop..."
        do! handleUserInput actorSystem

        do! serverComplete
    }

    Async.RunSynchronously(app)

    0