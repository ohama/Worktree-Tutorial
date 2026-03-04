namespace WorktreeApi.Products

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type Product =
        { Id: ProductId
          Name: string
          Description: string
          Price: decimal
          Stock: int
          CreatedAt: DateTime }

    type CreateProductRequest =
        { Name: string
          Description: string
          Price: decimal
          Stock: int }

    type UpdateProductRequest =
        { Name: string option
          Description: string option
          Price: decimal option
          Stock: int option }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, Product>()

    let create (req: CreateProductRequest) =
        if req.Price < 0m then
            Error "Price must be non-negative"
        elif req.Stock < 0 then
            Error "Stock must be non-negative"
        else
            let id = Guid.NewGuid()

            let product =
                { Id = ProductId id
                  Name = req.Name
                  Description = req.Description
                  Price = req.Price
                  Stock = req.Stock
                  CreatedAt = DateTime.UtcNow }

            store.[id] <- product
            Ok product

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, product -> Some product
        | false, _ -> None

    let update (id: Guid) (req: UpdateProductRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "Product not found"
        | true, product ->
            let updated =
                { product with
                    Name = req.Name |> Option.defaultValue product.Name
                    Description = req.Description |> Option.defaultValue product.Description
                    Price = req.Price |> Option.defaultValue product.Price
                    Stock = req.Stock |> Option.defaultValue product.Stock }

            store.[id] <- updated
            Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
