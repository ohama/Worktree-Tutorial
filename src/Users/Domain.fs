namespace WorktreeApi.Users

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type Role =
        | Admin
        | Member
        | Guest

    type User =
        { Id: UserId
          Name: string
          Email: string
          Role: Role
          CreatedAt: DateTime }

    type CreateUserRequest = { Name: string; Email: string; Role: string }

    type UpdateUserRequest =
        { Name: string option
          Email: string option
          Role: string option }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, User>()

    let parseRole =
        function
        | "admin" | "Admin" -> Some Admin
        | "member" | "Member" -> Some Member
        | "guest" | "Guest" -> Some Guest
        | _ -> None

    let create (req: CreateUserRequest) =
        match parseRole req.Role with
        | None -> Error "Invalid role. Use: admin, member, guest"
        | Some role ->
            let id = Guid.NewGuid()

            let user =
                { Id = UserId id
                  Name = req.Name
                  Email = req.Email
                  Role = role
                  CreatedAt = DateTime.UtcNow }

            store.[id] <- user
            Ok user

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, user -> Some user
        | false, _ -> None

    let update (id: Guid) (req: UpdateUserRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "User not found"
        | true, user ->
            let role =
                req.Role
                |> Option.bind parseRole
                |> Option.defaultValue user.Role

            let updated =
                { user with
                    Name = req.Name |> Option.defaultValue user.Name
                    Email = req.Email |> Option.defaultValue user.Email
                    Role = role }

            store.[id] <- updated
            Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
