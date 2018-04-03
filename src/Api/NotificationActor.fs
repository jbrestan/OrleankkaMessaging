namespace Api.Notifications

open FSharp.Control.Tasks
open Orleankka
open Orleankka.FSharp

type NotificationMessage = 
    | Send of string

type INotificationQueue =
    inherit IActorGrain<NotificationMessage>

type NotificationQueue() =
    inherit ActorGrain()
    interface INotificationQueue

    override this.Receive (messageObj: obj) = task {
        match messageObj with
        | :? NotificationMessage as msg ->
            match msg with
            | Send text ->
                printfn "Grain %s received a message: %s" this.Path.Id text
                let stream = ActorSystem.streamOf(this.System, "sms", this.Path.Id)
                do! stream.Push(text)
                return none ()
        |_ -> return unhandled()
    }