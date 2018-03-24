namespace Api

module HttpHandlers =

    open Microsoft.AspNetCore.Http
    open Giraffe
    open Api.Models

    let handleGetHello =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let response = {
                    Text = "Hello world, from Giraffe!"
                }
                return! json response next ctx
            }