using FluentAssertions;
using MiniDB.Core.Exceptions;
using MiniDB.Core.Types;
using System.Runtime.InteropServices;

namespace MiniDB.Tests.Types;

public class DbValueTests
{
    [Fact]
    public void DbValue_IsExactly8Bytes() =>
        Marshal.SizeOf<DbValue>().Should().Be(8);

    [Fact]
    public void TypedValue_IsExactly9Bytes() =>
        Marshal.SizeOf<TypedValue>().Should().Be(9);


    [Fact]
    public void Int64_RoundTrip()
    {
        var v = DbValue.FromInt64(42L);
        v.AsInt64().Should().Be(42L);
    }

    [Fact]
    public void Int64_Negative_RoundTrip()
    {
        var v = DbValue.FromInt64(-9_999_999_999L);
        v.AsInt64().Should().Be(-9_999_999_999L);
    }

    [Fact]
    public void Int64_MaxValue_RoundTrip()
    {
        var v = DbValue.FromInt64(long.MaxValue);
        v.AsInt64().Should().Be(long.MaxValue);
    }

    [Fact]
    public void Float64_RoundTrip()
    {
        var v = DbValue.FromFloat64(3.14159);
        v.AsFloat64().Should().Be(3.14159);
    }

    [Fact]
    public void Float64_NegativeInfinity_RoundTrip()
    {
        var v = DbValue.FromFloat64(double.NegativeInfinity);
        v.AsFloat64().Should().Be(double.NegativeInfinity);
    }

    [Fact]
    public void Float64_NaN_RoundTrip()
    {
        var v = DbValue.FromFloat64(double.NaN);
        double.IsNaN(v.AsFloat64()).Should().BeTrue();
    }

    [Fact]
    public void Bool_True_RoundTrip()
    {
        var v = DbValue.FromBool(true);
        v.AsBool().Should().BeTrue();
    }

    [Fact]
    public void Bool_False_RoundTrip()
    {
        var v = DbValue.FromBool(false);
        v.AsBool().Should().BeFalse();
    }

    [Fact]
    public void TextRef_RoundTrip()
    {
        var v = DbValue.FromTextRef(offset: 128, length: 64);
        var (off, len) = v.AsTextRef();

        off.Should().Be(128u);
        len.Should().Be(64u);
    }

    [Fact]
    public void TextRef_MaxValues_RoundTrip()
    {
        var v = DbValue.FromTextRef(uint.MaxValue, uint.MaxValue);
        var (off, len) = v.AsTextRef();

        off.Should().Be(uint.MaxValue);
        len.Should().Be(uint.MaxValue);
    }


    [Fact]
    public void Int64_DoesNotBleedIntoFloat()
    {
        var v = DbValue.FromInt64(1L);

        v.AsFloat64().Should().NotBe(1.0);
    }

    [Fact]
    public void Float64_DoesNotBleedIntoInt()
    {
        var v = DbValue.FromFloat64(1.0);

        v.AsInt64().Should().NotBe(1L);
    }

    [Fact]
    public void Null_IsAllZeros()
    {
        var v = DbValue.Null;
        v.RawBits().Should().Be(0UL);
    }

    [Fact]
    public void SameInt64_AreEqual()
    {
        DbValue.FromInt64(100)
            .Should()
            .Be(DbValue.FromInt64(100));
    }

    [Fact]
    public void DifferentInt64_AreNotEqual()
    {
        DbValue.FromInt64(1)
            .Should()
            .NotBe(DbValue.FromInt64(2));
    }

    [Fact]
    public void SameFloat_AreEqual()
    {
        DbValue.FromFloat64(1.5)
            .Should()
            .Be(DbValue.FromFloat64(1.5));
    }


    [Fact]
    public void TypedValue_Null_IsNull()
    {
        TypedValue.Null.IsNull.Should().BeTrue();
        TypedValue.Null.Type.Should().Be(DbType.Null);
    }

    [Fact]
    public void TypedValue_Int64_PreservesType()
    {
        var tv = TypedValue.Of(99L);

        tv.Type.Should().Be(DbType.Int64);
        tv.Value.AsInt64().Should().Be(99L);
    }


    [Fact]
    public void Compare_Int64_LessThan()
    {
        var a = TypedValue.Of(1L);
        var b = TypedValue.Of(2L);

        DbValueOps.Compare(a, b).Should().BeLessThan(0);
    }

    [Fact]
    public void Compare_NullIsLessThanAnything()
    {
        DbValueOps.Compare(TypedValue.Null, TypedValue.Of(0L))
            .Should()
            .BeLessThan(0);
    }

    [Fact]
    public void Compare_BothNull_AreEqual()
    {
        DbValueOps.Compare(TypedValue.Null, TypedValue.Null)
            .Should()
            .Be(0);
    }

    [Fact]
    public void Compare_IntAndFloat_CrossType()
    {
        var a = TypedValue.Of(2L);
        var b = TypedValue.Of(1.5);

        DbValueOps.Compare(a, b).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Add_TwoInts()
    {
        var result = DbValueOps.Add(TypedValue.Of(10L), TypedValue.Of(32L));

        result.Type.Should().Be(DbType.Int64);
        result.Value.AsInt64().Should().Be(42L);
    }

    [Fact]
    public void Add_NullPropagates()
    {
        var result = DbValueOps.Add(TypedValue.Null, TypedValue.Of(5L));

        result.IsNull.Should().BeTrue();
    }

    [Fact]
    public void Add_IntAndFloat_ReturnsFloat()
    {
        var result = DbValueOps.Add(TypedValue.Of(1L), TypedValue.Of(0.5));

        result.Type.Should().Be(DbType.Float64);
        result.Value.AsFloat64().Should().Be(1.5);
    }

    [Fact]
    public void Multiply_TwoFloats()
    {
        var result = DbValueOps.Multiply(TypedValue.Of(2.0), TypedValue.Of(3.5));

        result.Value.AsFloat64().Should().BeApproximately(7.0, 1e-10);
    }


    [Fact]
    public void Divide_TwoInts_Truncates()
    {
        var result = DbValueOps.Divide(TypedValue.Of(7L), TypedValue.Of(2L));
        result.Type.Should().Be(DbType.Int64);
        result.Value.AsInt64().Should().Be(3L);
    }

    [Fact]
    public void Divide_TwoFloats()
    {
        var result = DbValueOps.Divide(TypedValue.Of(7.0), TypedValue.Of(2.0));
        result.Type.Should().Be(DbType.Float64);
        result.Value.AsFloat64().Should().Be(3.5);
    }

    [Fact]
    public void Divide_IntByFloat_PromotesToFloat()
    {
        var result = DbValueOps.Divide(TypedValue.Of(1L), TypedValue.Of(4.0));
        result.Type.Should().Be(DbType.Float64);
        result.Value.AsFloat64().Should().Be(0.25);
    }

    [Fact]
    public void Divide_IntByZero_Throws()
    {
        var act = () => DbValueOps.Divide(
            TypedValue.Of(10L),
            TypedValue.Of(0L));
        act.Should()
           .Throw<DivisionByZeroException>()
           .WithMessage("Division by zero: 10 / 0");
    }

    [Fact]
    public void Divide_FloatByZero_Throws()
    {
        var act = () => DbValueOps.Divide(
        TypedValue.Of(10L),
        TypedValue.Of(0.0));

        act.Should()
           .Throw<DivisionByZeroException>();

    }

    [Fact]
    public void Divide_NullPropagates()
    {
        DbValueOps.Divide(TypedValue.Null, TypedValue.Of(5L)).IsNull.Should().BeTrue();
        DbValueOps.Divide(TypedValue.Of(5L), TypedValue.Null).IsNull.Should().BeTrue();
    }

    [Fact]
    public void Modulo_BasicRemainder()
    {
        var result = DbValueOps.Modulo(TypedValue.Of(10L), TypedValue.Of(3L));
        result.Value.AsInt64().Should().Be(1L);
    }

    [Fact]
    public void Modulo_ByZero_Throws()
    {
        var act = () => DbValueOps.Modulo(TypedValue.Of(10L), TypedValue.Of(0L));
        act.Should()
           .Throw<DivisionByZeroException>()
           .WithMessage("Modulo by zero: 10 % 0");
    }

    [Fact]
    public void Modulo_FloatOperand_Throws()
    {
        var act = () => DbValueOps.Modulo(TypedValue.Of(10.0), TypedValue.Of(3L));
        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage($"Modulo is only defined for Int64, got Float64 % Int64");
    }
}