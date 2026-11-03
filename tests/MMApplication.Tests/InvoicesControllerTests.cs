using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.DTOs;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class InvoicesControllerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private class FakeOcrService : IReceiptOcrService
    {
        public ExtractedReceiptDto Result { get; set; } = new()
        {
            MerchantName = "Zara - Al Nakheel Mall",
            InvoiceNumber = "INV-2026-001",
            InvoiceDate = DateTime.UtcNow,
            TotalAmount = 250.00m,
            Confidence = 0.98
        };

        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream)
        {
            return Task.FromResult(Result);
        }
    }

    private class FakeFileStorageService : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName)
        {
            return Task.FromResult($"/uploads/{folderName}/test_{fileName}");
        }

        public Task<bool> DeleteFileAsync(string relativeFilePath)
        {
            return Task.FromResult(true);
        }
    }

    private IFormFile CreateMockFormFile(string fileName = "receipt.jpg", string content = "fake image content")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    [Fact]
    public async Task UploadInvoice_WhenCustomerExistsAndInvoiceValid_ShouldSucceedAndAwardPoints()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-100",
            FullName = "سارة أحمد",
            PhoneNumber = "0921112233",
            CreatedAt = DateTime.UtcNow,
            Wallet = new CustomerWallet
            {
                CustomerId = "CUST-100",
                PointsBalance = 20,
                LastUpdated = DateTime.UtcNow
            }
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "H&M - Mall of Arabia",
                InvoiceNumber = "HM-9988",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 154.50m, // 154.50 / 10 = 15 points
                Confidence = 0.95
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-100",
            File = CreateMockFormFile("hm_receipt.png")
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var resultDto = Assert.IsType<InvoiceUploadResultDto>(okResult.Value);

        Assert.True(resultDto.Success);
        Assert.Equal(15, resultDto.PointsEarned); // 154.50 -> 15 points
        Assert.Equal(35, resultDto.CurrentWalletPointsBalance); // Initial 20 + 15 = 35

        // Verify database state
        var savedInvoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == "HM-9988");
        Assert.NotNull(savedInvoice);
        Assert.Equal("H&M - Mall of Arabia", savedInvoice.MerchantName);
        Assert.Equal(InvoiceStatus.APPROVED, savedInvoice.Status);
        Assert.Equal(154.50m, savedInvoice.TotalAmount);
        Assert.Equal(15, savedInvoice.PointsEarned);

        var updatedWallet = await context.CustomerWallets.FirstOrDefaultAsync(w => w.CustomerId == "CUST-100");
        Assert.NotNull(updatedWallet);
        Assert.Equal(35, updatedWallet.PointsBalance);
    }

    [Fact]
    public async Task UploadInvoice_WithAuthenticatedUserClaims_ShouldAutoExtractCustomerId()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-AUTH-AUTO",
            FullName = "ياسمين خالد",
            PhoneNumber = "0923334455",
            CreatedAt = DateTime.UtcNow,
            Wallet = new CustomerWallet
            {
                CustomerId = "CUST-AUTH-AUTO",
                PointsBalance = 10,
                LastUpdated = DateTime.UtcNow
            }
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "Carrefour",
                InvoiceNumber = "CRF-2026-1",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 200.00m,
                Confidence = 0.98
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimTypes.NameIdentifier, "CUST-AUTH-AUTO"),
            new(ClaimTypes.Role, "Customer")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        // Notice CustomerId is NULL in request, should be resolved from Claims
        var request = new InvoiceUploadRequest
        {
            CustomerId = null,
            File = CreateMockFormFile("carrefour_receipt.png")
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var resultDto = Assert.IsType<InvoiceUploadResultDto>(okResult.Value);

        Assert.True(resultDto.Success);
        Assert.Equal(20, resultDto.PointsEarned);
        Assert.Equal(30, resultDto.CurrentWalletPointsBalance); // 10 + 20 = 30
    }

    [Fact]
    public async Task UploadInvoice_WhenCustomerHasNoWallet_ShouldCreateWalletAndAwardPoints()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-NOWALLET",
            FullName = "محمد علي",
            PhoneNumber = "0929998877",
            CreatedAt = DateTime.UtcNow,
            Wallet = null // No initial wallet
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "Virgin Megastore",
                InvoiceNumber = "VIRGIN-101",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 299.99m, // 29 points
                Confidence = 0.99
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-NOWALLET",
            File = CreateMockFormFile("virgin_receipt.jpg")
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var resultDto = Assert.IsType<InvoiceUploadResultDto>(okResult.Value);

        Assert.True(resultDto.Success);
        Assert.Equal(29, resultDto.PointsEarned);
        Assert.Equal(29, resultDto.CurrentWalletPointsBalance);

        var wallet = await context.CustomerWallets.FirstOrDefaultAsync(w => w.CustomerId == "CUST-NOWALLET");
        Assert.NotNull(wallet);
        Assert.Equal(29, wallet.PointsBalance);
    }

    [Fact]
    public async Task UploadInvoice_WhenDuplicateInvoice_ShouldReturnConflict()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-200",
            FullName = "خالد عمر",
            PhoneNumber = "0934445566",
            CreatedAt = DateTime.UtcNow,
            Wallet = new CustomerWallet { CustomerId = "CUST-200", PointsBalance = 0 }
        };
        var existingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUST-200",
            MerchantName = "Zara - Mall",
            InvoiceNumber = "INV-DUPLICATE-01",
            InvoiceDate = DateTime.UtcNow,
            TotalAmount = 100.00m,
            PointsEarned = 10,
            Status = InvoiceStatus.APPROVED,
            CreatedAt = DateTime.UtcNow
        };

        await context.Customers.AddAsync(customer);
        await context.Invoices.AddAsync(existingInvoice);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "Zara - Mall",
                InvoiceNumber = "INV-DUPLICATE-01", // Same merchant + invoice number
                TotalAmount = 100.00m,
                Confidence = 0.95
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-200",
            File = CreateMockFormFile()
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task UploadInvoice_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var controller = new InvoicesController(
            context,
            new FakeOcrService(),
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "NON_EXISTING_CUSTOMER",
            File = CreateMockFormFile()
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task UploadInvoice_WhenFileExtensionNotAllowed_ShouldReturnBadRequest()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-300",
            FullName = "فاطمة علي",
            PhoneNumber = "0945556677",
            CreatedAt = DateTime.UtcNow
        };
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var controller = new InvoicesController(
            context,
            new FakeOcrService(),
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-300",
            File = CreateMockFormFile("document.exe") // Disallowed extension
        };

        // Act
        var actionResult = await controller.UploadInvoice(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task CustomersController_CreateAndGet_ShouldSucceed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var controller = new CustomersController(context, NullLogger<CustomersController>.Instance);

        var createDto = new CreateCustomerDto
        {
            FullName = "يوسف إبراهيم",
            PhoneNumber = "0910002233"
        };

        // Act
        var createResult = await controller.CreateCustomer(createDto);
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var customerDto = Assert.IsType<CustomerDto>(createdAction.Value);

        // Assert
        Assert.Equal("يوسف إبراهيم", customerDto.FullName);
        Assert.Equal("0910002233", customerDto.PhoneNumber);
        Assert.Equal(0, customerDto.PointsBalance);

        // Act 2: Fetch by ID
        var getResult = await controller.GetCustomerById(customerDto.Id);
        var okGet = Assert.IsType<OkObjectResult>(getResult);
        var fetchedDto = Assert.IsType<CustomerDto>(okGet.Value);

        Assert.Equal(customerDto.Id, fetchedDto.Id);
        Assert.Equal("يوسف إبراهيم", fetchedDto.FullName);
    }

    [Fact]
    public async Task UploadInvoice_WithStoreAlias_ShouldMatchTenantId()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var tenant = new Tenant
        {
            TenantId = "TEN-ZARA",
            Name = "Zara Mall of Tripoli",
            Category = "Fashion",
            FloorNumber = "1",
            UnitNumber = "101",
            LoyaltyType = StoreLoyaltyType.MallPoints,
            Aliases = new List<StoreAlias>
            {
                new StoreAlias { AliasName = "شركة الأفق للأزياء - فرع زارا", MatchType = "CONTAINS", IsActive = true }
            }
        };
        var customer = new Customer
        {
            Id = "CUST-501",
            FullName = "طارق المبروك",
            PhoneNumber = "0915554433",
            CreatedAt = DateTime.UtcNow
        };
        await context.Tenants.AddAsync(tenant);
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "شركة الأفق للأزياء - فرع زارا",
                InvoiceNumber = "OFQ-9901",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 200.00m,
                Confidence = 0.95
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-501",
            File = CreateMockFormFile("receipt_alias.jpg")
        };

        // Act
        var result = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == "OFQ-9901");
        Assert.NotNull(invoice);
        Assert.Equal("TEN-ZARA", invoice.TenantId);
    }

    [Fact]
    public async Task UploadInvoice_WithProductBoosterRule_ShouldAwardBonusPoints()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var booster = new ProductBoosterRule
        {
            Title = "بونص عصير المراعي",
            BrandOrItemKeyword = "Almarai",
            BonusPoints = 25,
            PointsMultiplier = 1.0m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            IsActive = true
        };
        var customer = new Customer
        {
            Id = "CUST-502",
            FullName = "مروان الشريف",
            PhoneNumber = "0917778899",
            CreatedAt = DateTime.UtcNow
        };
        await context.ProductBoosterRules.AddAsync(booster);
        await context.Customers.AddAsync(customer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "سوبرماركت المدينة",
                InvoiceNumber = "SUP-101",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 100.00m, // Base: 10 points
                Confidence = 0.95,
                Items = new List<ExtractedReceiptItemDto>
                {
                    new ExtractedReceiptItemDto { Description = "Almarai Fresh Milk 1L", Quantity = 2, TotalPrice = 12m }
                }
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-502",
            File = CreateMockFormFile("receipt_booster.jpg")
        };

        // Act
        var result = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == "SUP-101");
        Assert.NotNull(invoice);
        // Total points should be: Base (10) + Booster Bonus (25) = 35 points
        Assert.Equal(35, invoice.PointsAwarded);
    }

    [Fact]
    public async Task UploadInvoice_WithMultiAccountDevice_ShouldFlagReviewRequired()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var devFp = "DEVICE-FINGERPRINT-UNIQUE-777";
        
        // Two prior customers already used this device
        await context.CustomerDevices.AddRangeAsync(
            new CustomerDevice { CustomerId = "CUST-A", DeviceFingerprint = devFp, DeviceModel = "iPhone 15" },
            new CustomerDevice { CustomerId = "CUST-B", DeviceFingerprint = devFp, DeviceModel = "iPhone 15" }
        );

        var newCustomer = new Customer
        {
            Id = "CUST-C",
            FullName = "حساب ثالث مشبوه",
            PhoneNumber = "0919990001",
            CreatedAt = DateTime.UtcNow
        };
        await context.Customers.AddAsync(newCustomer);
        await context.SaveChangesAsync();

        var fakeOcr = new FakeOcrService
        {
            Result = new ExtractedReceiptDto
            {
                MerchantName = "متجر الإلكترونيات",
                InvoiceNumber = "ELEC-500",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 80.00m,
                Confidence = 0.95
            }
        };

        var controller = new InvoicesController(
            context,
            fakeOcr,
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var request = new InvoiceUploadRequest
        {
            CustomerId = "CUST-C",
            DeviceFingerprint = devFp,
            DeviceModel = "iPhone 15",
            File = CreateMockFormFile("receipt_device.jpg")
        };

        // Act
        var result = await controller.UploadInvoice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == "ELEC-500");
        Assert.NotNull(invoice);
        // Anti-sybil should have flagged this for human review
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, invoice.ProcessingStatus);
        Assert.Contains("الجهاز", invoice.RejectionReason);
    }

    [Fact]
    public async Task CustomerDispute_WhenInvoiceRejected_ShouldSetStatusToDisputed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customer = new Customer
        {
            Id = "CUST-DISP",
            FullName = "عميل معترض",
            PhoneNumber = "0918881122",
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = "CUST-DISP",
            InvoiceNumber = "REJ-001",
            ProcessingStatus = InvoiceStatus.REJECTED,
            RejectionReason = "صورة غير واضحة",
            TotalAmount = 150m,
            CreatedAt = DateTime.UtcNow
        };
        await context.Customers.AddAsync(customer);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        var controller = new InvoicesController(
            context,
            new FakeOcrService(),
            new FakeFileStorageService(),
            new FlexibleLoyaltyEvaluationService(context),
            NullLogger<InvoicesController>.Instance);

        var disputeRequest = new CustomerDisputeRequest
        {
            DisputeReason = "الفاتورة واضحة جداً والمحل من ضمن المول"
        };

        // Act
        var result = await controller.DisputeInvoice(invoice.InvoiceId, disputeRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedInv = await context.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInv);
        Assert.Equal(InvoiceStatus.DISPUTED, updatedInv.ProcessingStatus);
        Assert.Equal("الفاتورة واضحة جداً والمحل من ضمن المول", updatedInv.DisputeReason);
    }
}

