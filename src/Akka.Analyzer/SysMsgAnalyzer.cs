using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using DiagnosticDescriptor = Microsoft.CodeAnalysis.DiagnosticDescriptor;
using DiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;
using LanguageNames = Microsoft.CodeAnalysis.LanguageNames;
using LocalizableResourceString = Microsoft.CodeAnalysis.LocalizableResourceString;
using LocalizableString = Microsoft.CodeAnalysis.LocalizableString;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Akka.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SysMsgAnalyzer : DiagnosticAnalyzer
    {
        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.SysMsgAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.SysMsgAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.SysMsgAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private const string TargetMethodName = "Tell"; //the "Tell" method on the ICanTell interface
        private const string ICanTellMethodFQN = "Akka.Actor.ICanTell.Tell";
        private const string IActorRefTellMethodFQN = "Akka.Actor.IActorRef.Tell";
        private const string ActorRefImplicitSenderTellFQN = "Akka.Actor.ActorRefImplicitSenderExtensions.Tell";
        private const string ISystemMsgFQN = "Akka.Dispatch.SysMsg.ISystemMessage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticIds.SystemMessageSentViaTellRuleId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeTell, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeTell(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            // technically this should be a MemberAccessExpression, since it's invoked as a method off of IActorRef / ICanTell
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpr?.Name.ToString() != TargetMethodName) return;

            // need to verify that this `Tell` method is actually `ICanTell.Tell`
            var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;


            var memberIsICanTell = (memberSymbol?.ToString().StartsWith(ICanTellMethodFQN) ?? false) || (memberSymbol?.ToString().StartsWith(IActorRefTellMethodFQN) ?? false);
            var memberIsActorRefExtension = memberSymbol?.ToString().StartsWith(ActorRefImplicitSenderTellFQN) ?? false;
            if (!memberIsICanTell && !memberIsActorRefExtension) return;


            // need to get the first argument being passed to the `Tell` method
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
            if ((argumentList?.Arguments.Count ?? 0) < 1) return;

            // ReSharper disable once PossibleNullReferenceException //already validated as not null above
            var messageArgument =
                context.SemanticModel.GetSymbolInfo(memberIsICanTell ? argumentList.Arguments[0].Expression : argumentList.Arguments[1].Expression).Symbol;
            var allInterfaces = ((messageArgument as INamedTypeSymbol)?.Interfaces ?? ImmutableArray<INamedTypeSymbol>.Empty).Concat(messageArgument?.ContainingType.Interfaces ?? ImmutableArray<INamedTypeSymbol>.Empty).ToImmutableArray();
            if (allInterfaces.Any(y => y.ToString().StartsWith(ISystemMsgFQN)))
            {
                // found an ISystemMessage being passed into a Tell method
                var diagnostic = Diagnostic.Create(Rule, argumentList.GetLocation(), messageArgument.ToString(), DiagnosticSeverity.Error);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
