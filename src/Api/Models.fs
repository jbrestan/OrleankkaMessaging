namespace Api.Models

[<CLIMutable>]
type Message =
    {
        clientId : string
        text : string
    }

[<CLIMutable>]
type Response =
    {
        messageId : string
    }