﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Defines the set of type-marker values that are used to encode JSON value
        /// </summary>
        public readonly struct TypeMarker
        {
            #region [0x00, 0x20): Encoded literal integer value (32 values)
            /// <summary>
            /// The first integer what can be encoded in the type marker itself.
            /// </summary>
            /// <example>1 can be encoded as LiterIntMin + 1.</example>
            public const byte LiteralIntMin = 0x00;

            /// <summary>
            /// The last integer what can be encoded in the type marker itself.
            /// </summary>
            /// <example>1 can be encoded as LiterIntMin + 1.</example>
            public const byte LiteralIntMax = LiteralIntMin + 32;
            #endregion

            #region [0x20, 0x40): Encoded 1-byte system string (32 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte SystemString1ByteLengthMin = LiteralIntMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte SystemString1ByteLengthMax = SystemString1ByteLengthMin + 32;
            #endregion

            #region [0x40, 0x60): Encoded 1-byte user string (32 values)
            /// <summary>
            /// The first type marker for a user string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte UserString1ByteLengthMin = SystemString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a user string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte UserString1ByteLengthMax = UserString1ByteLengthMin + 32;
            #endregion

            #region [0x60, 0x80): 2-byte user string (32 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMin = UserString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMax = UserString2ByteLengthMin + 32;
            #endregion

            #region [0x80, 0xC0): Encoded string length (64 values)
            /// <summary>
            /// The first type marker for a string whose length is encoded.
            /// </summary>
            /// <example>EncodedStringLengthMin + 1 is a type marker for a string with length 1.</example>
            public const byte EncodedStringLengthMin = UserString2ByteLengthMax;

            /// <summary>
            /// The last type marker for a string whose length is encoded.
            /// </summary>
            /// <example>EncodedStringLengthMin + 1 is a type marker for a string with length 1.</example>
            public const byte EncodedStringLengthMax = EncodedStringLengthMin + 64;
            #endregion

            #region [0xC0, 0xC8): Variable Length Strings and Binary Values
            /// <summary>
            /// Type marker for a String of 1-byte length
            /// </summary>
            public const byte String1ByteLength = 0xC0;

            /// <summary>
            /// Type marker for a String of 2-byte length
            /// </summary>
            public const byte String2ByteLength = 0xC1;

            /// <summary>
            /// Type marker for a String of 4-byte length
            /// </summary>
            public const byte String4ByteLength = 0xC2;

            /// <summary>
            /// Type marker for a Compressed string of 1-byte length
            /// </summary>
            public const byte Binary1ByteLength = 0xC3;

            /// <summary>
            /// Type marker for a Compressed string of 2-byte length
            /// </summary>
            public const byte Binary2ByteLength = 0xC4;

            /// <summary>
            /// Type marker for a Compressed string of 4-byte length
            /// </summary>
            public const byte Binary4ByteLength = 0xC5;

            // <empty> 0xC6
            // <empty> 0xC7
            #endregion

            #region [0xC8, 0xD0): Number Values
            /// <summary>
            /// Type marker for a 1-byte unsigned integer
            /// </summary>
            public const byte NumberUInt8 = 0xC8;

            /// <summary>
            /// Type marker for a 2-byte singed integer
            /// </summary>
            public const byte NumberInt16 = 0xC9;

            /// <summary>
            /// Type marker for a 4-byte singed integer
            /// </summary>
            public const byte NumberInt32 = 0xCA;

            /// <summary>
            /// Type marker for a 8-byte singed integer
            /// </summary>
            public const byte NumberInt64 = 0xCB;

            /// <summary>
            /// Type marker for a Double-precession floating point number
            /// </summary>
            public const byte NumberDouble = 0xCC;

            /// <summary>
            /// Type marker for a single precision floating point number.
            /// </summary>
            public const byte Float32 = 0xCD;

            /// <summary>
            /// Type marker for double precision floating point number.
            /// </summary>
            public const byte Float64 = 0xCE;

            // <number reserved> 0xCF
            #endregion

            #region [0xDO, 0xE0): Other Value Types
            /// <summary>
            /// The type marker for a JSON null value.
            /// </summary>
            public const byte Null = 0xD0;

            /// <summary>
            /// The type marker for a JSON false value.
            /// </summary>
            public const byte False = 0xD1;

            /// <summary>
            /// The type marker for a JSON true value
            /// </summary>
            public const byte True = 0xD2;

            /// <summary>
            /// The type marker for a GUID
            /// </summary>
            public const byte Guid = 0xD3;

            // <other types empty> 0xD4
            // <other types empty> 0xD5
            // <other types empty> 0xD6
            // <other types empty> 0xD7

            /// <summary>
            /// The type marker for a 1-byte signed integer value.
            /// </summary>
            public const byte Int8 = 0xD8;

            /// <summary>
            /// The type marker for a 2-byte signed integer value.
            /// </summary>
            public const byte Int16 = 0xD9;

            /// <summary>
            /// The type marker for a 4-byte signed integer value.
            /// </summary>
            public const byte Int32 = 0xDA;

            /// <summary>
            /// The type marker for a 8-byte signed integer value.
            /// </summary>
            public const byte Int64 = 0xDB;

            /// <summary>
            /// The type marker for a 4-byte signed integer value.
            /// </summary>
            public const byte UInt32 = 0xDC;

            // <other types reserved> 0xDD
            // <other types reserved> 0xDE
            // <other types reserved> 0xDF
            #endregion

            #region [0xE0, 0xE8): Array Type Markers

            /// <summary>
            /// Empty array type marker.
            /// </summary>
            public const byte EmptyArray = 0xE0;

            /// <summary>
            /// Single-item array type marker.
            /// </summary>
            public const byte SingleItemArray = 0xE1;

            /// <summary>
            /// Array of 1-byte length type marker.
            /// </summary>
            public const byte Array1ByteLength = 0xE2;

            /// <summary>
            /// Array of 2-byte length type marker.
            /// </summary>
            public const byte Array2ByteLength = 0xE3;

            /// <summary>
            /// Array of 4-byte length type marker.
            /// </summary>
            public const byte Array4ByteLength = 0xE4;

            /// <summary>
            /// Array of 1-byte length and item count type marker.
            /// </summary>
            public const byte Array1ByteLengthAndCount = 0xE5;

            /// <summary>
            /// Array of 2-byte length and item count type marker.
            /// </summary>
            public const byte Array2ByteLengthAndCount = 0xE6;

            /// <summary>
            /// Array of 4-byte length and item count type marker.
            /// </summary>
            public const byte Array4ByteLengthAndCount = 0xE7;
            #endregion

            #region [0xE8, 0xF0): Object Type Markers
            /// <summary>
            /// Empty object type marker.
            /// </summary>
            public const byte EmptyObject = 0xE8;

            /// <summary>
            /// Single-property object type marker.
            /// </summary>
            public const byte SinglePropertyObject = 0xE9;

            /// <summary>
            /// Object of 1-byte length type marker.
            /// </summary>
            public const byte Object1ByteLength = 0xEA;

            /// <summary>
            /// Object of 2-byte length type marker.
            /// </summary>
            public const byte Object2ByteLength = 0xEB;

            /// <summary>
            /// Object of 4-byte length type maker.
            /// </summary>
            public const byte Object4ByteLength = 0xEC;

            /// <summary>
            /// Object of 1-byte length and property count type marker.
            /// </summary>
            public const byte Object1ByteLengthAndCount = 0xED;

            /// <summary>
            /// Object of 2-byte length and property count type marker.
            /// </summary>
            public const byte Object2ByteLengthAndCount = 0xEE;

            /// <summary>
            /// Object of 4-byte length and property count type marker.
            /// </summary>
            public const byte Object4ByteLengthAndCount = 0xEF;
            #endregion

            #region [0xF0, 0xF8): Empty Range
            // <empty> 0xF0
            // <empty> 0xF1
            // <empty> 0xF2
            // <empty> 0xF3
            // <empty> 0xF4
            // <empty> 0xF5
            // <empty> 0xF7
            #endregion

            #region [0xF8, 0xFF]: Special Values
            // <special value reserved> 0xF8
            // <special value reserved> 0xF9
            // <special value reserved> 0xFA
            // <special value reserved> 0xFB
            // <special value reserved> 0xFC
            // <special value reserved> 0xFD
            // <special value reserved> 0xFE

            /// <summary>
            /// Type marker reserved to communicate an invalid type marker.
            /// </summary>
            public const byte Invalid = 0xFF;
            #endregion

            #region Number Type Marker Utility Functions
            /// <summary>
            /// Gets whether an integer can be encoded as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer can be encoded as a literal.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedNumberLiteral(long value) => InRange(value, LiteralIntMin, LiteralIntMax);

            /// <summary>
            /// Gets whether an integer is a fixed length integer.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a fixed length integer.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsFixedLengthNumber(long value) => InRange(value, NumberUInt8, NumberDouble + 1);

            /// <summary>
            /// Gets whether an integer is a number.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a number.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNumber(long value) => IsEncodedNumberLiteral(value) || IsFixedLengthNumber(value);

            /// <summary>
            /// Encodes an integer as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>The integer encoded as a literal if it can; else Invalid</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte EncodeIntegerLiteral(long value) => IsEncodedNumberLiteral(value) ? (byte)(LiteralIntMin + value) : Invalid;
            #endregion

            #region String Type Markers Utility Functions
            /// <summary>
            /// Gets whether a typeMarker is for a system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a system string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSystemString(byte typeMarker) => InRange(typeMarker, SystemString1ByteLengthMin, SystemString1ByteLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsOneByteEncodedUserString(byte typeMarker) => InRange(typeMarker, UserString1ByteLengthMin, UserString1ByteLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTwoByteEncodedUserString(byte typeMarker) => InRange(typeMarker, UserString2ByteLengthMin, UserString2ByteLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for a user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsUserString(byte typeMarker) => IsOneByteEncodedUserString(typeMarker) || IsTwoByteEncodedUserString(typeMarker);

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsOneByteEncodedString(byte typeMarker) => InRange(typeMarker, SystemString1ByteLengthMin, UserString1ByteLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTwoByteEncodedString(byte typeMarker) => IsTwoByteEncodedUserString(typeMarker);

            /// <summary>
            /// Gets whether a typeMarker is for an encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedString(byte typeMarker) => InRange(typeMarker, SystemString1ByteLengthMin, UserString2ByteLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for an encoded length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedLengthString(byte typeMarker) => InRange(typeMarker, EncodedStringLengthMin, EncodedStringLengthMax);

            /// <summary>
            /// Gets whether a typeMarker is for a variable length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a variable length string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsVarLengthString(byte typeMarker) => InRange(typeMarker, String1ByteLength, String4ByteLength + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a variable length compressed string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a variable length compressed string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsVarLengthCompressedString(byte typeMarker) => InRange(typeMarker, Binary1ByteLength, Binary4ByteLength + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a string.
            /// </summary>
            /// <param name="typeMarker">The type maker.</param>
            /// <returns>Whether the typeMarker is for a string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsString(byte typeMarker) => InRange(typeMarker, SystemString1ByteLengthMin, Binary4ByteLength + 1);

            /// <summary>
            /// Gets the length of a encoded string type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>The length of the encoded string type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long GetEncodedStringLength(byte typeMarker) => typeMarker & (EncodedStringLengthMin - 1);

            /// <summary>
            /// Gets the type marker for an encoded string of a particular length.
            /// </summary>
            /// <param name="length">The length of the encoded string.</param>
            /// <returns>The type marker for an encoded string of a particular length.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte GetEncodedStringLengthTypeMarker(long length) => length < (EncodedStringLengthMax - EncodedStringLengthMin) ? (byte)(length | EncodedStringLengthMin) : Invalid;
            #endregion

            #region Other Primitive Type Markers Utility Functions
            /// <summary>
            /// Gets whether a type maker is the null type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the null type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNull(byte typeMarker) => typeMarker == Null;

            /// <summary>
            /// Gets whether a type maker is the false type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the false type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsFalse(byte typeMarker) => typeMarker == False;

            /// <summary>
            /// Gets whether a type maker is the true type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the true type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTrue(byte typeMarker) => typeMarker == True;

            /// <summary>
            /// Gets whether a type maker is a boolean type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is a boolean type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsBoolean(byte typeMarker) => (typeMarker == False) || (typeMarker == True);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsGuid(byte typeMarker) => typeMarker == Guid;
            #endregion

            #region Array/Object Type Markers
            /// <summary>
            /// Gets whether a type marker is the empty array type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is the empty array type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEmptyArray(byte typeMarker) => typeMarker == EmptyArray;

            /// <summary>
            /// Gets whether a type marker is for an array.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an array.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsArray(byte typeMarker) => InRange(typeMarker, EmptyArray, Array4ByteLengthAndCount + 1);

            /// <summary>
            /// Gets whether a type marker is the empty object type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is the empty object type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEmptyObject(byte typeMarker) => typeMarker == EmptyObject;
            /// <summary>
            /// Gets whether a type marker is for an object.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an object.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsObject(byte typeMarker) => InRange(typeMarker, EmptyObject, Object4ByteLengthAndCount + 1);
            #endregion

            #region Common Utility Functions
            /// <summary>
            /// Gets whether a type marker is valid.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is valid.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsValid(byte typeMarker) => typeMarker != Invalid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool InRange(long value, long minInclusive, long maxExclusive) => (value >= minInclusive) && (value < maxExclusive);
            #endregion
        }
    }
}
