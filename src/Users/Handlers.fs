namespace WorktreeApi.Users

open System
open Microsoft.AspNetCore.Http
open Giraffe
open WorktreeApi.Core

module Handlers =

    let getAll: HttpHandler =
        fun next ctx ->
            let users = Domain.getAll ()
            json (ApiResponse.success users) next ctx

    let getById (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | Some user -> json (ApiResponse.success user) next ctx
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error "User not found") next ctx

    let create: HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.CreateUserRequest>()

                match Domain.create req with
                | Ok user ->
                    ctx.SetStatusCode 201
                    return! json (ApiResponse.success user) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let update (id: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.UpdateUserRequest>()

                match Domain.update id req with
                | Ok user -> return! json (ApiResponse.success user) next ctx
                | Error msg ->
                    ctx.SetStatusCode 404
                    return! json (ApiResponse.error msg) next ctx
            }

    let delete (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error (sprintf "User %O not found" id)) next ctx
            | Some _ ->
                Domain.delete id |> ignore
                ctx.SetStatusCode 204
                next ctx

    let routes: HttpHandler =
        subRoute
            "/api/users"
            (choose
                [ GET
                  >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PUT >=> routef "/%O" update
                  DELETE >=> routef "/%O" delete ])
