namespace WorktreeApi

open System

module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

    // === API Response Wrapper ===
    type ApiResponse<'T> =
        { Data: 'T option
          Message: string
          Success: bool }

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
