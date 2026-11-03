namespace MMApplication.Tests;

public class PointsCalculationTests
{
    [Theory]
    [InlineData(0.00, 0)]
    [InlineData(5.50, 0)]
    [InlineData(9.99, 0)]
    [InlineData(10.00, 1)]
    [InlineData(19.99, 1)]
    [InlineData(20.00, 2)]
    [InlineData(154.50, 15)]
    [InlineData(299.99, 29)]
    [InlineData(1000.00, 100)]
    [InlineData(1234.56, 123)]
    public void CalculatePoints_ShouldAwardOnePointPerTenUnits(decimal totalAmount, int expectedPoints)
    {
        // Act: 1 point per 10 currency units
        var points = (int)Math.Floor(totalAmount / 10m);

        // Assert
        Assert.Equal(expectedPoints, points);
    }
}
