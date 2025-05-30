// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: InputSequenceToC.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
/// <summary>Holder for reflection information generated from InputSequenceToC.proto</summary>
public static partial class InputSequenceToCReflection {

  #region Descriptor
  /// <summary>File descriptor for InputSequenceToC.proto</summary>
  public static pbr::FileDescriptor Descriptor {
    get { return descriptor; }
  }
  private static pbr::FileDescriptor descriptor;

  static InputSequenceToCReflection() {
    byte[] descriptorData = global::System.Convert.FromBase64String(
        string.Concat(
          "ChZJbnB1dFNlcXVlbmNlVG9DLnByb3RvGhNJbnB1dFNlcXVlbmNlLnByb3Rv",
          "IksKEElucHV0U2VxdWVuY2VUb0MSEAoIZW50aXR5SWQYASABKAkSJQoNaW5w",
          "dXRTZXF1ZW5jZRgCIAEoCzIOLklucHV0U2VxdWVuY2ViBnByb3RvMw=="));
    descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
        new pbr::FileDescriptor[] { global::InputSequenceReflection.Descriptor, },
        new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
          new pbr::GeneratedClrTypeInfo(typeof(global::InputSequenceToC), global::InputSequenceToC.Parser, new[]{ "EntityId", "InputSequence" }, null, null, null, null)
        }));
  }
  #endregion

}
#region Messages
/// <summary>
/// @auto_generate 
/// </summary>
[global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
public sealed partial class InputSequenceToC : pb::IMessage<InputSequenceToC>
#if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    , pb::IBufferMessage
#endif
{
  private static readonly pb::MessageParser<InputSequenceToC> _parser = new pb::MessageParser<InputSequenceToC>(() => new InputSequenceToC());
  private pb::UnknownFieldSet _unknownFields;
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public static pb::MessageParser<InputSequenceToC> Parser { get { return _parser; } }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public static pbr::MessageDescriptor Descriptor {
    get { return global::InputSequenceToCReflection.Descriptor.MessageTypes[0]; }
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  pbr::MessageDescriptor pb::IMessage.Descriptor {
    get { return Descriptor; }
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public InputSequenceToC() {
    OnConstruction();
  }

  partial void OnConstruction();

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public InputSequenceToC(InputSequenceToC other) : this() {
    entityId_ = other.entityId_;
    inputSequence_ = other.inputSequence_ != null ? other.inputSequence_.Clone() : null;
    _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public InputSequenceToC Clone() {
    return new InputSequenceToC(this);
  }

  /// <summary>Field number for the "entityId" field.</summary>
  public const int EntityIdFieldNumber = 1;
  private string entityId_ = "";
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public string EntityId {
    get { return entityId_; }
    set {
      entityId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
    }
  }

  /// <summary>Field number for the "inputSequence" field.</summary>
  public const int InputSequenceFieldNumber = 2;
  private global::InputSequence inputSequence_;
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public global::InputSequence InputSequence {
    get { return inputSequence_; }
    set {
      inputSequence_ = value;
    }
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public override bool Equals(object other) {
    return Equals(other as InputSequenceToC);
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public bool Equals(InputSequenceToC other) {
    if (ReferenceEquals(other, null)) {
      return false;
    }
    if (ReferenceEquals(other, this)) {
      return true;
    }
    if (EntityId != other.EntityId) return false;
    if (!object.Equals(InputSequence, other.InputSequence)) return false;
    return Equals(_unknownFields, other._unknownFields);
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public override int GetHashCode() {
    int hash = 1;
    if (EntityId.Length != 0) hash ^= EntityId.GetHashCode();
    if (inputSequence_ != null) hash ^= InputSequence.GetHashCode();
    if (_unknownFields != null) {
      hash ^= _unknownFields.GetHashCode();
    }
    return hash;
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public override string ToString() {
    return pb::JsonFormatter.ToDiagnosticString(this);
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public void WriteTo(pb::CodedOutputStream output) {
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    output.WriteRawMessage(this);
  #else
    if (EntityId.Length != 0) {
      output.WriteRawTag(10);
      output.WriteString(EntityId);
    }
    if (inputSequence_ != null) {
      output.WriteRawTag(18);
      output.WriteMessage(InputSequence);
    }
    if (_unknownFields != null) {
      _unknownFields.WriteTo(output);
    }
  #endif
  }

  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
    if (EntityId.Length != 0) {
      output.WriteRawTag(10);
      output.WriteString(EntityId);
    }
    if (inputSequence_ != null) {
      output.WriteRawTag(18);
      output.WriteMessage(InputSequence);
    }
    if (_unknownFields != null) {
      _unknownFields.WriteTo(ref output);
    }
  }
  #endif

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public int CalculateSize() {
    int size = 0;
    if (EntityId.Length != 0) {
      size += 1 + pb::CodedOutputStream.ComputeStringSize(EntityId);
    }
    if (inputSequence_ != null) {
      size += 1 + pb::CodedOutputStream.ComputeMessageSize(InputSequence);
    }
    if (_unknownFields != null) {
      size += _unknownFields.CalculateSize();
    }
    return size;
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public void MergeFrom(InputSequenceToC other) {
    if (other == null) {
      return;
    }
    if (other.EntityId.Length != 0) {
      EntityId = other.EntityId;
    }
    if (other.inputSequence_ != null) {
      if (inputSequence_ == null) {
        InputSequence = new global::InputSequence();
      }
      InputSequence.MergeFrom(other.InputSequence);
    }
    _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
  }

  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  public void MergeFrom(pb::CodedInputStream input) {
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    input.ReadRawMessage(this);
  #else
    uint tag;
    while ((tag = input.ReadTag()) != 0) {
    if ((tag & 7) == 4) {
      // Abort on any end group tag.
      return;
    }
    switch(tag) {
        default:
          _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
          break;
        case 10: {
          EntityId = input.ReadString();
          break;
        }
        case 18: {
          if (inputSequence_ == null) {
            InputSequence = new global::InputSequence();
          }
          input.ReadMessage(InputSequence);
          break;
        }
      }
    }
  #endif
  }

  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
  [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
  [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
  void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
    uint tag;
    while ((tag = input.ReadTag()) != 0) {
    if ((tag & 7) == 4) {
      // Abort on any end group tag.
      return;
    }
    switch(tag) {
        default:
          _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
          break;
        case 10: {
          EntityId = input.ReadString();
          break;
        }
        case 18: {
          if (inputSequence_ == null) {
            InputSequence = new global::InputSequence();
          }
          input.ReadMessage(InputSequence);
          break;
        }
      }
    }
  }
  #endif

}

#endregion


#endregion Designer generated code
