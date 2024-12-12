using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LibEthernetIPStack.Shared;

[JsonConverter(typeof(StringEnumConverter))]
public enum EnIPNetworkStatus { OnLine, OnLineReadRejected, OnLineWriteRejected, OnLineForwardOpenReject, OffLine };
public enum EnIPForwardOpenStatus { ForwardOpen, ForwardClose };

