namespace Api.Notifications

open FSharp.Control.Tasks
open Orleankka
open Orleankka.FSharp

type NotificationMessage = 
    | Send of string

type NotificationQueue() =
    inherit ActorGrain()
    interface IActorGrain<NotificationMessage>

    override this.Receive (messageObj: obj) = task {
        match messageObj with
        | :? NotificationMessage as msg -> 
            match msg with
            | Send text ->
                printfn "Received: %s" text
                return none ()
        |_ -> return unhandled()            
    }