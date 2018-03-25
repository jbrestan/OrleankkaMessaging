namespace Api

module HttpHandlers =

    open Microsoft.AspNetCore.Http
    open Giraffe
    open Api.Models

    let handleSendMessage messageConsumer =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! request = ctx.BindJsonAsync<Message>()
                try
                    let messageId = System.Guid.NewGuid() |> string
                    let! result = messageConsumer messageId request
                    match result with
                    | Ok _ ->
                        let response = {
                            MessageId = "Hello world, from Giraffe!"
                        }
                        return! json response next ctx
                    | Error errorText ->
                        ctx.SetStatusCode(500)
                        return! text errorText next ctx
                with e ->
                    ctx.SetStatusCode(500)
                    return! text e.Message next ctx
            }