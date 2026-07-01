using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Domain.Customers;
using Binesh.Domain.Products;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

public sealed class CompiledSelectorCacheTests
{
    [Fact]
    public void Get_ReturnsSameDelegateOnRepeatCall()
    {
        var cache = new CompiledSelectorCache();
        var schema = CustomerSchema.Build();
        var field = schema.GetField("PaymentReliability");

        var first = cache.Get(typeof(Customer), field);
        var second = cache.Get(typeof(Customer), field);

        Assert.Same(first, second);
    }

    [Fact]
    public void Get_DifferentFields_ReturnDifferentDelegates()
    {
        var cache = new CompiledSelectorCache();
        var schema = CustomerSchema.Build();

        var a = cache.Get(typeof(Customer), schema.GetField("PaymentReliability"));
        var b = cache.Get(typeof(Customer), schema.GetField("Active"));

        Assert.NotSame(a, b);
    }

    [Fact]
    public void Get_FlatField_ExtractsValueFromInstance()
    {
        var cache = new CompiledSelectorCache();
        var schema = CustomerSchema.Build();
        var paymentReliability = schema.GetField("PaymentReliability");

        var person = Person.Create("Ali", "Ahmadi", null, null, "0911", null, null, null, null, null);
        var customer = Customer.Create(CustomerType.MoshtarianKhanegi, true, 0.75f, person);

        var getter = cache.Get(typeof(Customer), paymentReliability);
        Assert.Equal(0.75f, getter(customer));
    }

    [Fact]
    public void Get_NavigationField_FollowsPath()
    {
        var cache = new CompiledSelectorCache();
        var schema = CustomerSchema.Build();
        var mobile = schema.GetField("Mobile");

        var person = Person.Create("Ali", "Ahmadi", null, null, "+989121234567", null, null, null, null, null);
        var customer = Customer.Create(CustomerType.MoshtarianKhanegi, true, 0.5f, person);

        var getter = cache.Get(typeof(Customer), mobile);
        Assert.Equal("+989121234567", getter(customer));
    }

    [Fact]
    public void Get_EnumField_ReturnsEnumInstance()
    {
        var cache = new CompiledSelectorCache();
        var schema = CustomerSchema.Build();
        var type = schema.GetField("CustomerType");

        var person = Person.Create("X", null, null, null, null, null, null, null, null, null);
        var customer = Customer.Create(CustomerType.Personnel, true, 0.5f, person);

        var getter = cache.Get(typeof(Customer), type);
        Assert.Equal(CustomerType.Personnel, getter(customer));
    }

    [Fact]
    public void Get_WrongEntityType_Throws()
    {
        var cache = new CompiledSelectorCache();
        var customerField = CustomerSchema.Build().GetField("Active");

        // Trying to use the Customer selector against a Product entity.
        Assert.Throws<InvalidOperationException>(
            () => cache.Get(typeof(Product), customerField));
    }
}
