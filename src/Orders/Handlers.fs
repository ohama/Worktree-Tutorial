namespace WorktreeApi.Orders

open System
open Microsoft.AspNetCore.Http
open Giraffe
open WorktreeApi.Core

module Handlers =

    let getAll: HttpHandler =
        fun next ctx ->
            let orders = Domain.getAll ()
            json (ApiResponse.success orders) next ctx

    let getById (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | Some order -> json (ApiResponse.success order) next ctx
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error "Order not found") next ctx

    let create: HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.CreateOrderRequest>()

                match Domain.create req with
                | Ok order ->
                    ctx.SetStatusCode 201
                    return! json (ApiResponse.success order) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let updateStatus (id: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.UpdateOrderStatusRequest>()

                match Domain.updateStatus id req with
                | Ok order -> return! json (ApiResponse.success order) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let delete (id: Guid) : HttpHandler =
        fun next ctx ->
            if Domain.delete id then
                ctx.SetStatusCode 204
                next ctx
            else
                ctx.SetStatusCode 404
                json (ApiResponse.error "Order not found") next ctx

    let routes: HttpHandler =
        subRoute
            "/api/orders"
            (choose
                [ GET
                  >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PATCH >=> routef "/%O" updateStatus
                  DELETE >=> routef "/%O" delete ])
