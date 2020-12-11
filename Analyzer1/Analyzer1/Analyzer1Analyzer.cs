using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Analyzer1
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Analyzer1Analyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Analyzer1";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";
        private const string HelpLinkUri = "https://github.com";
        private List<Term> terms;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            if (terms is null)
            {
                var currentDirecory = GetFolderTypeWasLoadedFrom<Analyzer1Analyzer>();
                terms = JsonSerializer.Deserialize<List<Term>>(File.ReadAllBytes(Path.Combine(currentDirecory, "terms-en.json")));
            }

            // Analyze symbols
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field,
                    SymbolKind.Event, SymbolKind.Namespace, SymbolKind.Parameter);

        }

        private static string GetFolderTypeWasLoadedFrom<T>()
            => new FileInfo(new Uri(typeof(T).Assembly.CodeBase).LocalPath).Directory.FullName;

        private static bool ContainsUnsafeWords(string symbol, string term)
        {
            return term.Length < 4 ?
                symbol.Equals(term, StringComparison.InvariantCultureIgnoreCase) :
                symbol.IndexOf(term, StringComparison.InvariantCultureIgnoreCase) >= 0;

        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            try
            {
                var symbol = context.Symbol;

                foreach (var term in terms)
                {
                    if (ContainsUnsafeWords(symbol.Name, term.Name))
                    {
                        var diag = Diagnostic.Create(GetRule(term, symbol.Name), symbol.Locations[0], term.Name, symbol.Name, term.Severity, term.Recommendation);
                        context.ReportDiagnostic(diag);
                        break;
                    }
                }
            }
            catch { }
        }

        private static DiagnosticDescriptor GetRule(Term term, string identifier)
        {
            var warningLevel = DiagnosticSeverity.Info;
            var description = $"Recommendation: {term.Recommendation}{System.Environment.NewLine}Reason: {term.Why}";
            switch (term.Severity)
            {
                case "1":
                    warningLevel = DiagnosticSeverity.Error;
                    break;
                case "2":
                case "3":
                case "4":
                    warningLevel = DiagnosticSeverity.Warning;
                    break;
                default:
                    warningLevel = DiagnosticSeverity.Info;
                    break;
            }

            return new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, warningLevel, isEnabledByDefault: true, description: description, helpLinkUri: HelpLinkUri, term.Name);
        }
    }
}
