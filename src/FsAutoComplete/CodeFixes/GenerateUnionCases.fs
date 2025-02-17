module FsAutoComplete.CodeFix.GenerateUnionCases

open FsToolkit.ErrorHandling
open FsAutoComplete.CodeFix
open FsAutoComplete.CodeFix.Types
open Ionide.LanguageServerProtocol.Types
open FsAutoComplete
open FsAutoComplete.LspHelpers
open FsAutoComplete.CodeFix.Navigation

let title = "Generate union pattern match cases"

/// a codefix that generates union cases for an incomplete match expression
let fix
  (getFileLines: GetFileLines)
  (getParseResultsForFile: GetParseResultsForFile)
  (generateCases: _ -> _ -> _ -> _ -> Async<CoreResponse<_>>)
  (getTextReplacements: unit -> Map<string, string>)
  =
  Run.ifDiagnosticByCode (Set.ofList [ "25" ]) (fun diagnostic codeActionParams ->
    asyncResult {
      let fileName = codeActionParams.TextDocument.GetFilePath() |> Utils.normalizePath

      let! lines = getFileLines fileName
      // try to find the first case already written
      let fcsRange = protocolRangeToRange (FSharp.UMX.UMX.untag fileName) diagnostic.Range

      let! nextLine = lines.NextLine fcsRange.Start |> Result.ofOption (fun _ -> "no next line")

      let! caseLine = lines.GetLine(nextLine) |> Result.ofOption (fun _ -> "No case line")

      let caseCol = caseLine.IndexOf('|') + 3 // Find column of first case in pattern matching

      let casePos =
        { Line = nextLine.Line - 1
          Character = caseCol }

      let casePosFCS = protocolPosToPos casePos

      let! (tyRes, line, lines) = getParseResultsForFile fileName casePosFCS

      match! generateCases tyRes casePosFCS lines line |> Async.map Ok with
      | CoreResponse.Res(insertString: string, insertPosition) ->
        let range =
          { Start = fcsPosToLsp insertPosition
            End = fcsPosToLsp insertPosition }

        let replacements = getTextReplacements ()

        let replaced =
          (insertString, replacements)
          ||> Seq.fold (fun text (KeyValue(key, replacement)) -> text.Replace(key, replacement))

        return
          [ { SourceDiagnostic = Some diagnostic
              File = codeActionParams.TextDocument
              Title = title
              Edits = [| { Range = range; NewText = replaced } |]
              Kind = FixKind.Fix } ]

      | _ -> return []
    })
