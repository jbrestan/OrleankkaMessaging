module Api.App

open System
open System.Reflection
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

        do! startWebServer actorSystem |> Async.AwaitTask
    }

    Async.RunSynchronously(app)

    0