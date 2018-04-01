module Api.App

open System
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Api.HttpHandlers

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/send-message" >=> handleSendMessage (fun _ _ -> task { return Ok ()})
                ]
            ])
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseCors(configureCors)
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

let startWebServer () =
    WebHostBuilder()
        .UseKestrel()
        .UseIISIntegration()
        .Configure(Action<IApplicationBuilder>(configureApp))
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .RunAsync()

open Orleankka
open Orleankka.Client
open Orleankka.Cluster
open Orleankka.FSharp
open Orleans.Hosting

open FSharp.Control.Tasks

let host () = task {
    let sb = new SiloHostBuilder()
    sb.AddAssembly(Assembly.GetExecutingAssembly())
    sb.ConfigureOrleankka() |> ignore

    return! sb.Start()
}
let client (host: ISiloHost) = host.Connect()

open Api.Notifications

let demo () = task {
    use! h = host ()
    let! client = client h

    let system = client.ActorSystem()
    let actor = ActorSystem.typedActorOf<INotificationQueue, NotificationMessage>(system, "demo-queue")
    do! actor <! Send "yo!"
}

[<EntryPoint>]
let main _ =
    async {
        do! demo () |> Async.AwaitTask

        do! startWebServer () |> Async.AwaitTask
    } |> Async.RunSynchronously

    0