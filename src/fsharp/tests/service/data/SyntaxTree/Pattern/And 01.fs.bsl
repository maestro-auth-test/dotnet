ImplFile
  (ParsedImplFileInput
     ("/root/Pattern/And 01.fs", false, QualifiedNameOfFile Module, [],
      [SynModuleOrNamespace
         ([Module], false, NamedModule,
          [Let
             (false,
              [SynBinding
                 (None, Normal, false, false, [],
                  PreXmlDoc ((3,0), FSharp.Compiler.Xml.XmlDocCollector),
                  SynValData
                    (None,
                     SynValInfo
                       ([[SynArgInfo ([], false, Some x)]],
                        SynArgInfo ([], false, None)), None),
                  LongIdent
                    (SynLongIdent ([f], [], [None]), None, None,
                     Pats [Named (SynIdent (x, None), false, None, (3,6--3,7))],
                     None, (3,4--3,7)), None,
                  Match
                    (Yes (4,4--4,16), Ident x,
                     [SynMatchClause
                        (LongIdent
                           (SynLongIdent ([Error], [], [None]), None, None,
                            Pats
                              [Paren
                                 (Ands
                                    ([Typed
                                        (Wild (5,13--5,14),
                                         LongIdent
                                           (SynLongIdent ([exn], [], [None])),
                                         (5,13--5,19));
                                      IsInst
                                        (LongIdent
                                           (SynLongIdent
                                              ([System; NullReferenceException],
                                               [(5,31--5,32)], [None; None])),
                                         (5,22--5,54))], (5,13--5,54)),
                                  (5,12--5,55))], None, (5,6--5,55)), None,
                         Const (Unit, (5,59--5,61)), (5,6--5,61), Yes,
                         { ArrowRange = Some (5,56--5,58)
                           BarRange = Some (5,4--5,5) });
                      SynMatchClause
                        (Wild (6,6--6,7), None, Const (Unit, (6,11--6,13)),
                         (6,6--6,13), Yes, { ArrowRange = Some (6,8--6,10)
                                             BarRange = Some (6,4--6,5) })],
                     (4,4--6,13), { MatchKeyword = (4,4--4,9)
                                    WithKeyword = (4,12--4,16) }), (3,4--3,7),
                  NoneAtLet, { LeadingKeyword = Let (3,0--3,3)
                               InlineKeyword = None
                               EqualsRange = Some (3,8--3,9) })], (3,0--6,13))],
          PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), [], None,
          (1,0--6,13), { LeadingKeyword = Module (1,0--1,6) })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))
