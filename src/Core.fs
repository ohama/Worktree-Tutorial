namespace WorktreeApi

open System

module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

    // === Order Status ===
    type OrderStatus =
        | Pending
        | Confirmed
        | Shipped
        | Delivered
        | Cancelled

    // === API Response Wrapper ===
    type ApiResponse<'T> =
        { Data: 'T option
          Message: string
          Success: bool }

    // === Paginated Response ===
    type PaginatedResponse<'T> =
        { Data: 'T list
          Page: int
          PageSize: int
          TotalCount: int
          TotalPages: int }

    module ApiResponse =
        let success data =
            { Data = Some data
              Message = "OK"
              Success = true }

        let error msg =
            { Data = None
              Message = msg
              Success = false }

        let noContent () =
            { Data = None
              Message = "No Content"
              Success = true }

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
