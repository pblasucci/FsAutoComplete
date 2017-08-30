﻿namespace FsAutoComplete

open System

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open FsAutoComplete.UnopenedNamespacesResolver
open FSharpLint.Application

module internal CompletionUtils =
  let map =
    [ 0x0000,  ("Class", "C")
      0x0003,  ("Enum", "E")
      0x00012, ("Struct", "S")
      0x00018, ("Struct", "S") (* value type *)
      0x0002,  ("Delegate", "D")
      0x0008,  ("Interface", "I")
      0x000e,  ("Module", "N") (* module *)
      0x000f,  ("Namespace", "N")
      0x000c,  ("Method", "M")
      0x000d,  ("Extension Method", "M") (* method2 ? *)
      0x00011, ("Property", "P")
      0x0005,  ("Event", "e")
      0x0007,  ("Field", "F") (* fieldblue ? *)
      0x0020,  ("Field", "Fy") (* fieldyellow ? *)
      0x0001,  ("Function", "Fc") (* const *)
      0x0004,  ("Property", "P") (* enummember *)
      0x0006,  ("Exception", "X") (* exception *)
      0x0009,  ("Text File Icon", "t") (* TextLine *)
      0x000a,  ("Regular File", "R") (* Script *)
      0x000b,  ("Script", "s") (* Script2 *)
      0x0010,  ("Tip of the day", "t") (* Formula *);
      0x00013, ("Class", "C") (* Template *)
      0x00014, ("Class", "C") (* Typedef *)
      0x00015, ("Type", "T") (* Type *)
      0x00016, ("Type", "T") (* Union *)
      0x00017, ("Field", "V") (* Variable *)
      0x00019, ("Class", "C") (* Intrinsic *)
      0x0001f, ("Other", "o") (* error *)
      0x00021, ("Other", "o") (* Misc1 *)
      0x0022,  ("Other", "o") (* Misc2 *)
      0x00023, ("Other", "o") (* Misc3 *) ] |> Map.ofSeq

  let getIcon glyph =
    match map.TryFind (glyph / 6), map.TryFind (glyph % 6) with
    | Some(s), _ -> s // Is the second number good for anything?
    | _, _ -> ("", "")

  let getEnclosingEntityChar = function
    | FSharpEnclosingEntityKind.Namespace -> "N"
    | FSharpEnclosingEntityKind.Module -> "M"
    | FSharpEnclosingEntityKind.Class -> "C"
    | FSharpEnclosingEntityKind.Exception -> "E"
    | FSharpEnclosingEntityKind.Interface -> "I"
    | FSharpEnclosingEntityKind.Record -> "R"
    | FSharpEnclosingEntityKind.Enum -> "En"
    | FSharpEnclosingEntityKind.DU -> "D"

module CommandResponse =

  type ResponseMsg<'T> =
    {
      Kind: string
      Data: 'T
    }

  type Location =
    {
      File: string
      Line: int
      Column: int
    }

  type CompletionResponse =
    {
      Name: string
      ReplacementText: string
      Glyph: string
      GlyphChar: string
    }

  type ResponseError<'T> =
    {
      Code: int
      Message: string
      AdditionalData: 'T
    }

  [<RequireQualifiedAccess>]
  type ErrorCodes =
    | GenericError = 1
    | ProjectNotRestored = 100

  [<RequireQualifiedAccess>]
  type ErrorData =
    | GenericError
    | ProjectNotRestored of ProjectNotRestoredData
  and ProjectNotRestoredData = { Project: ProjectFilePath }

  [<RequireQualifiedAccess>]
  type ProjectResponseInfo =
    | DotnetSdk of ProjectResponseInfoDotnetSdk
    | Verbose
    | ProjectJson
  and ProjectResponseInfoDotnetSdk =
    {
      IsTestProject: bool
      Configuration: string
      IsPackable: bool
      TargetFramework: string
      TargetFrameworkIdentifier: string
      TargetFrameworkVersion: string
      RestoreSuccess: bool
      TargetFrameworks: string list
      RunCmd: RunCmd option
      IsPublishable: bool option
    }
  and [<RequireQualifiedAccess>] RunCmd = { Command: string; Arguments: string }

  type ProjectResponse =
    {
      Project: ProjectFilePath
      Files: List<SourceFilePath>
      Output: string
      References: List<ProjectFilePath>
      Logs: Map<string, string>
      OutputType: ProjectOutputType
      Info: ProjectResponseInfo
      AdditionalInfo: Map<string, string>
    }

  type OverloadDescription =
    {
      Signature: string
      Comment: string
    }

  type OverloadParameter =
    {
      Name : string
      CanonicalTypeTextForSorting : string
      Display : string
      Description : string
    }
  type Overload =
    {
      Tip : OverloadDescription list list
      TypeText : string
      Parameters : OverloadParameter list
      IsStaticArguments : bool
    }
  type MethodResponse =
    {
      Name : string
      CurrentParameter : int
      Overloads : Overload list
    }

  type SymbolUseRange =
    {
      FileName: string
      StartLine: int
      StartColumn: int
      EndLine: int
      EndColumn: int
      IsFromDefinition: bool
      IsFromAttribute : bool
      IsFromComputationExpression : bool
      IsFromDispatchSlotImplementation : bool
      IsFromPattern : bool
      IsFromType : bool
    }

  type SymbolUseResponse =
    {
      Name: string
      Uses: SymbolUseRange list
    }



  type HelpTextResponse =
    {
      Name: string
      Overloads: OverloadDescription list list
    }

  type CompilerLocationResponse =
    {
      Fsc: string
      Fsi: string
      MSBuild: string
    }

  type FSharpErrorInfo =
    {
      FileName: string
      StartLine:int
      EndLine:int
      StartColumn:int
      EndColumn:int
      Severity:FSharpErrorSeverity
      Message:string
      Subcategory:string
    }
    static member OfFSharpError(e:Microsoft.FSharp.Compiler.FSharpErrorInfo) =
      {
        FileName = e.FileName
        StartLine = e.StartLineAlternate
        EndLine = e.EndLineAlternate
        StartColumn = e.StartColumn + 1
        EndColumn = e.EndColumn + 1
        Severity = e.Severity
        Message = e.Message
        Subcategory = e.Subcategory
      }

  type ErrorResponse =
    {
      File: string
      Errors: FSharpErrorInfo []
    }

  type Colorization =
    {
      Range: Range.range
      Kind: string
    }

  type Declaration =
    {
      UniqueName: string
      Name: string
      Glyph: string
      GlyphChar: string
      IsTopLevel: bool
      Range: Range.range
      BodyRange : Range.range
      File : string
      EnclosingEntity: string
      IsAbstract: bool
    }
    static member OfDeclarationItem(e:FSharpNavigationDeclarationItem, fn) =
      let (glyph, glyphChar) = CompletionUtils.getIcon e.Glyph
      {
        UniqueName = e.UniqueName
        Name = e.Name
        Glyph = glyph
        GlyphChar = glyphChar
        IsTopLevel = e.IsSingleTopLevel
        Range = e.Range
        BodyRange = e.BodyRange
        File = fn
        EnclosingEntity = CompletionUtils.getEnclosingEntityChar e.EnclosingEntityKind
        IsAbstract = e.IsAbstract
      }

  type DeclarationResponse = {
      Declaration : Declaration;
      Nested : Declaration []
  }

  type OpenNamespace = {
    Namespace : string
    Name : string
    Type : string
    Line : int
    Column : int
    MultipleNames : bool
  }

  type QualifySymbol = {
    Name : string
    Qualifier : string
  }

  type ResolveNamespaceResponse = {
    Opens : OpenNamespace []
    Qualifies: QualifySymbol []
    Word : string
  }

  type UnionCaseResponse = {
    Text : string
    Position : Pos
  }

  type Parameter = {
    Name : string
    Type : string
  }

  type SignatureData = {
    OutputType : string
    Parameters : Parameter list list
  }

  type WorkspacePeekResponse = {
    Found: WorkspacePeekFound list
  }
  and WorkspacePeekFound =
    | Directory of WorkspacePeekFoundDirectory
    | Solution of WorkspacePeekFoundSolution
  and WorkspacePeekFoundDirectory = {
    Directory: string
    Fsprojs: string list
  }
  and WorkspacePeekFoundSolution = {
    Path: string
    Items: WorkspacePeekFoundSolutionItem list
    Configurations: WorkspacePeekFoundSolutionConfiguration list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItem = {
    Guid: Guid
    Name: string
    Kind: WorkspacePeekFoundSolutionItemKind
  }
  and WorkspacePeekFoundSolutionItemKind =
    | MsbuildFormat of WorkspacePeekFoundSolutionItemKindMsbuildFormat
    | Folder of WorkspacePeekFoundSolutionItemKindFolder
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItemKindMsbuildFormat = {
    Configurations: WorkspacePeekFoundSolutionConfiguration list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionItemKindFolder = {
    Items: WorkspacePeekFoundSolutionItem list
    Files: FilePath list
  }
  and [<RequireQualifiedAccess>] WorkspacePeekFoundSolutionConfiguration = {
    Id: string
    ConfigurationName: string
    PlatformName: string
  }

  let info (serialize : Serializer) (s: string) = serialize { Kind = "info"; Data = s }

  let errorG (serialize : Serializer) (errorData: ErrorData) message =
    let inline ser code data =
        serialize { Kind = "error"; Data = { Code = (int code); Message = message; AdditionalData = data }  }
    match errorData with
    | ErrorData.GenericError -> ser (ErrorCodes.GenericError) (obj())
    | ErrorData.ProjectNotRestored d -> ser (ErrorCodes.ProjectNotRestored) d

  let error (serialize : Serializer) (s: string) = errorG serialize ErrorData.GenericError s

  let helpText (serialize : Serializer) (name: string, tip: FSharpToolTipText) =
    let data = TipFormatter.formatTip tip |> List.map(List.map(fun (n,m) -> {Signature = n; Comment = m} ))
    serialize { Kind = "helptext"; Data = { HelpTextResponse.Name = name; Overloads = data } }

  let project (serialize : Serializer) (projectFileName, projectFiles, outFileOpt, references, logMap, (extra: ExtraProjectInfoData), additionals) =
    let projectInfo =
      match extra.ProjectSdkType with
      | ProjectSdkType.Verbose ->
        ProjectResponseInfo.Verbose
      | ProjectSdkType.ProjectJson ->
        ProjectResponseInfo.ProjectJson
      | ProjectSdkType.DotnetSdk info ->
        ProjectResponseInfo.DotnetSdk { 
          IsTestProject = info.IsTestProject
          Configuration = info.Configuration
          IsPackable = info.IsPackable
          TargetFramework = info.TargetFramework
          TargetFrameworkIdentifier = info.TargetFrameworkIdentifier
          TargetFrameworkVersion = info.TargetFrameworkVersion
          RestoreSuccess = info.RestoreSuccess
          TargetFrameworks = info.TargetFrameworks          
          RunCmd =
            match info.RunCommand, info.RunArguments with
            | Some cmd, Some args -> Some { RunCmd.Command = cmd; Arguments = args }
            | Some cmd, None -> Some { RunCmd.Command = cmd; Arguments = "" }
            | _ -> None
          IsPublishable = info.IsPublishable
        }
    let projectData =
      { Project = projectFileName
        Files = projectFiles
        Output = match outFileOpt with Some x -> x | None -> "null"
        References = List.sortBy IO.Path.GetFileName references
        Logs = logMap
        OutputType = extra.ProjectOutputType
        Info = projectInfo
        AdditionalInfo = additionals }
    serialize { Kind = "project"; Data = projectData }

  let projectError (serialize : Serializer) errorDetails =
    match errorDetails with
    | GenericError errorMessage -> error serialize errorMessage //compatibility with old api
    | ProjectNotRestored project -> errorG serialize (ErrorData.ProjectNotRestored { Project = project }) "Project not restored"

  let workspacePeek (serialize : Serializer) (found: FsAutoComplete.WorkspacePeek.Interesting list) =
    let mapInt i =
        match i with
        | FsAutoComplete.WorkspacePeek.Interesting.Directory (p, fsprojs) ->
            WorkspacePeekFound.Directory { WorkspacePeekFoundDirectory.Directory = p; Fsprojs = fsprojs }
        | FsAutoComplete.WorkspacePeek.Interesting.Solution (p, sd) ->
            let rec item (x: FsAutoComplete.WorkspacePeek.SolutionItem) =
                let kind =
                    match x.Kind with
                    | FsAutoComplete.WorkspacePeek.SolutionItemKind.Unknown
                    | FsAutoComplete.WorkspacePeek.SolutionItemKind.Unsupported ->
                        None
                    | FsAutoComplete.WorkspacePeek.SolutionItemKind.MsbuildFormat msbuildProj ->
                        Some (WorkspacePeekFoundSolutionItemKind.MsbuildFormat { 
                            WorkspacePeekFoundSolutionItemKindMsbuildFormat.Configurations = [] 
                        })
                    | FsAutoComplete.WorkspacePeek.SolutionItemKind.Folder(children, files) ->
                        let c = children |> List.choose item
                        Some (WorkspacePeekFoundSolutionItemKind.Folder { 
                            WorkspacePeekFoundSolutionItemKindFolder.Items = c
                            Files = files
                        })
                kind
                |> Option.map (fun k -> { WorkspacePeekFoundSolutionItem.Guid = x.Guid; Name = x.Name; Kind = k })
            let items = sd.Items |> List.choose item
            WorkspacePeekFound.Solution { WorkspacePeekFoundSolution.Path = p; Items = items; Configurations = [] }

    let data = { WorkspacePeekResponse.Found = found |> List.map mapInt }
    serialize { Kind = "workspacePeek"; Data = data }

  let completion (serialize : Serializer) (decls: FSharpDeclarationListItem[]) includeKeywords =
      serialize {  Kind = "completion"
                   Data = [ for d in decls do
                               let code = Microsoft.FSharp.Compiler.SourceCodeServices.PrettyNaming.QuoteIdentifierIfNeeded d.Name
                               let (glyph, glyphChar) = CompletionUtils.getIcon d.Glyph
                               yield {CompletionResponse.Name = d.Name; ReplacementText = code; Glyph = glyph; GlyphChar = glyphChar }
                            if includeKeywords then
                              for k in KeywordList.allKeywords do
                                yield {CompletionResponse.Name = k; ReplacementText = k; Glyph = "Keyword"; GlyphChar = "K"}
                          ] }

  let symbolUse (serialize : Serializer) (symbol: FSharpSymbolUse, uses: FSharpSymbolUse[]) =
    let su =
      { Name = symbol.Symbol.DisplayName
        Uses =
          [ for su in uses do
              yield { StartLine = su.RangeAlternate.StartLine
                      StartColumn = su.RangeAlternate.StartColumn + 1
                      EndLine = su.RangeAlternate.EndLine
                      EndColumn = su.RangeAlternate.EndColumn + 1
                      FileName = su.FileName
                      IsFromDefinition = su.IsFromDefinition
                      IsFromAttribute = su.IsFromAttribute
                      IsFromComputationExpression = su.IsFromComputationExpression
                      IsFromDispatchSlotImplementation = su.IsFromDispatchSlotImplementation
                      IsFromPattern = su.IsFromPattern
                      IsFromType = su.IsFromType } ] |> Seq.distinct |> Seq.toList }
    serialize { Kind = "symboluse"; Data = su }

  let signatureData (serialize : Serializer) ((typ, parms) : string * ((string * string) list list) ) =
    let pms =
      parms
      |> List.map (List.map (fun (n, t) -> { Name= n; Type = t }))
    serialize { Kind = "signatureData"; Data = { Parameters = pms; OutputType = typ } }

  let help (serialize : Serializer) (data : string) =
    serialize { Kind = "help"; Data = data }

  let methods (serialize : Serializer) (meth: FSharpMethodGroup, commas: int) =
      serialize {  Kind = "method"
                   Data = {  Name = meth.MethodName
                             CurrentParameter = commas
                             Overloads =
                              [ for o in meth.Methods do
                                 let tip = TipFormatter.formatTip o.Description |> List.map(List.map(fun (n,m) -> {Signature = n; Comment = m} ))
                                 yield {
                                   Tip = tip
                                   TypeText = o.TypeText
                                   Parameters =
                                     [ for p in o.Parameters do
                                        yield {
                                          Name = p.ParameterName
                                          CanonicalTypeTextForSorting = p.CanonicalTypeTextForSorting
                                          Display = p.Display
                                          Description = p.Description
                                        }
                                   ]
                                   IsStaticArguments = not o.HasParameters
                                 }
                              ] }
                }

  let errors (serialize : Serializer) (errors: Microsoft.FSharp.Compiler.FSharpErrorInfo[], file: string) =
    serialize { Kind = "errors";
                Data = { File = file
                         Errors = Array.map FSharpErrorInfo.OfFSharpError errors }}

  let colorizations (serialize : Serializer) (colorizations: (Range.range * SemanticClassificationType)[]) =
    // let data = [ for r, k in colorizations do
    //                yield { Range = r; Kind = Enum.GetName(typeof<SemanticClassificationType>, k) } ]
    serialize { Kind = "colorizations"; Data = [] } //TODO: Fix colorization

  let findDeclaration (serialize : Serializer) (range: Range.range) =
    let data = { Line = range.StartLine; Column = range.StartColumn + 1; File = range.FileName }
    serialize { Kind = "finddecl"; Data = data }

  let declarations (serialize : Serializer) (decls : (FSharpNavigationTopLevelDeclaration * string) []) =
     let decls' =
      decls |> Array.map (fun (d, fn) ->
        { Declaration = Declaration.OfDeclarationItem (d.Declaration, fn);
          Nested = d.Nested |> Array.map ( fun a -> Declaration.OfDeclarationItem(a,fn))
        })
     serialize { Kind = "declarations"; Data = decls' }

  let toolTip (serialize : Serializer) (tip) =
    let data = TipFormatter.formatTip tip |> List.map(List.map(fun (n,m) -> {Signature = n; Comment = m} ))
    serialize { Kind = "tooltip"; Data = data }

  let typeSig (serialize : Serializer) (tip) =
    let data = TipFormatter.extractSignature tip
    serialize { Kind = "typesig"; Data = data }

  let compilerLocation (serialize : Serializer) fsc fsi msbuild =
    let data = { Fsi = fsi; Fsc = fsc; MSBuild = msbuild }
    serialize { Kind = "compilerlocation"; Data = data }

  let message (serialize : Serializer) (kind: string, data: 'a) =
    serialize { Kind = kind; Data = data }

  let lint (serialize : Serializer) (warnings : LintWarning.Warning list) =
    let data = warnings |> List.toArray

    serialize { Kind = "lint"; Data = data }


  let resolveNamespace (serialize : Serializer) (word: string, opens : (string * string * InsertContext * bool) list, qualfies : (string * string) list) =
    let ops =
      opens
      |> List.map (fun (ns, name, ctx, multiple) ->
        {
          Namespace = ns
          Name = name
          Type = ctx.ScopeKind.ToString()
          Line = ctx.Pos.Line
          Column = ctx.Pos.Col
          MultipleNames = multiple
        })
      |> List.toArray

    let quals =
      qualfies
      |> List.map (fun (name, q) ->
        {
          QualifySymbol.Name = name
          QualifySymbol.Qualifier = q
        })
      |> List.toArray

    let data = {
      Opens = ops
      Qualifies = quals
      Word = word
    }

    serialize { Kind = "namespaces"; Data = data}

  let unionCase (serialize : Serializer) (text : string) position =
    let data = {
      Text = text
      Position = position
    }
    serialize { Kind = "unionCase"; Data = data}

