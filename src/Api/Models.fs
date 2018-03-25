namespace Api.Models

[<CLIMutable>]
type Message =
    {
        ClientId : string
        Text : string
    }

[<CLIMutable>]
type Response =
    {
        MessageId : string
    }