@echo off

::Generation C# classes
protogen -i:RequestExecution.proto -o:RequestExecution.cs
protogen -i:SignalMT4Trade.proto -o:SignalMT4Trade.cs
protogen -i:RequestOrdersStatus.proto -o:RequestOrdersStatus.cs
protogen -i:SignalOrdersStatus.proto -o:SignalOrdersStatus.cs
protogen -i:Request.proto -o:Request.cs
protogen -i:Signal.proto -o:Signal.cs

::Generation c++ classes
protoc --proto_path=. --cpp_out=. *.proto
