Akka.Analyzer
==========================
A [Roslyn Analyzer](https://github.com/dotnet/roslyn) suite for Akka.NET projects.

Currently supports the following checks:

* **Error**: tried to send `ISystemMessage` through `IActorRef.Tell` instead of `IInternalActorRef.SendSystemMessage`

Want to add more? Suggest [some analyzers you'd like to see here](https://github.com/akkadotnet/AkkaAnalyzer/issues/2).