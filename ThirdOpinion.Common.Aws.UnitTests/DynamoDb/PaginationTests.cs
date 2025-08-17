using ThirdOpinion.Common.Aws.DynamoDb.Pagination;

namespace ThirdOpinion.Common.Aws.Tests.DynamoDb;

public class PaginationTests
{
    [Fact]
    public void GetPageUri_ValidParameters_ReturnsCorrectUri()
    {
        // Arrange
        var parameters = new PaginationQuery(2) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";

        // Act
        var result = PaginationHelper.GetPageUri(parameters, baseUri, route);

        // Assert
        Assert.Equal("https://api.example.com/users?pageNumber=2&pageSize=10", result.ToString());
    }

    [Fact]
    public void CreatePagedResponse_FirstPage_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string> { "item1", "item2", "item3" };
        var parameters = new PaginationQuery(1) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";
        var totalRecords = 25;

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route, totalRecords);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Metadata.CurrentPage.ShouldBe(1);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBe(25);
        result.Metadata.TotalPages.ShouldBe(3);
        result.Metadata.HasPrevious.ShouldBeFalse();
        result.Metadata.HasNext.ShouldBeTrue();
        
        // Check links
        result.Metadata.Links.Keys.ShouldContain("nextPage");
        result.Metadata.Links.Keys.ShouldContain("firstPage");
        result.Metadata.Links.Keys.ShouldContain("lastPage");
        result.Metadata.Links.Keys.ShouldNotContain("previousPage");
    }

    [Fact]
    public void CreatePagedResponse_MiddlePage_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string> { "item11", "item12", "item13" };
        var parameters = new PaginationQuery(2) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";
        var totalRecords = 25;

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route, totalRecords);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Metadata.CurrentPage.ShouldBe(2);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBe(25);
        result.Metadata.TotalPages.ShouldBe(3);
        result.Metadata.HasPrevious.ShouldBeTrue();
        result.Metadata.HasNext.ShouldBeTrue();
        
        // Check links
        result.Metadata.Links.Keys.ShouldContain("previousPage");
        result.Metadata.Links.Keys.ShouldContain("nextPage");
        result.Metadata.Links.Keys.ShouldContain("firstPage");
        result.Metadata.Links.Keys.ShouldContain("lastPage");
    }

    [Fact]
    public void CreatePagedResponse_LastPage_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string> { "item21", "item22", "item23", "item24", "item25" };
        var parameters = new PaginationQuery(3) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";
        var totalRecords = 25;

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route, totalRecords);

        // Assert
        result.Items.Count.ShouldBe(5);
        result.Metadata.CurrentPage.ShouldBe(3);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBe(25);
        result.Metadata.TotalPages.ShouldBe(3);
        result.Metadata.HasPrevious.ShouldBeTrue();
        result.Metadata.HasNext.ShouldBeFalse();
        
        // Check links
        result.Metadata.Links.Keys.ShouldContain("previousPage");
        result.Metadata.Links.Keys.ShouldContain("firstPage");
        result.Metadata.Links.Keys.ShouldContain("lastPage");
        result.Metadata.Links.Keys.ShouldNotContain("nextPage");
    }

    [Fact]
    public void CreatePagedResponse_EmptyResults_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string>();
        var parameters = new PaginationQuery(1) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";
        var totalRecords = 0;

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route, totalRecords);

        // Assert
        result.Items.ShouldBeEmpty();
        result.Metadata.CurrentPage.ShouldBe(1);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBe(0);
        result.Metadata.TotalPages.ShouldBe(0);
        result.Metadata.HasPrevious.ShouldBeFalse();
        result.Metadata.HasNext.ShouldBeFalse();
    }

    [Fact]
    public void CreatePagedResponse_SinglePage_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string> { "item1", "item2", "item3" };
        var parameters = new PaginationQuery(1) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";
        var totalRecords = 3;

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route, totalRecords);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Metadata.CurrentPage.ShouldBe(1);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBe(3);
        result.Metadata.TotalPages.ShouldBe(1);
        result.Metadata.HasPrevious.ShouldBeFalse();
        result.Metadata.HasNext.ShouldBeFalse();
        
        // Check links
        result.Metadata.Links.Keys.ShouldContain("firstPage");
        result.Metadata.Links.Keys.ShouldContain("lastPage");
        result.Metadata.Links.Keys.ShouldNotContain("previousPage");
        result.Metadata.Links.Keys.ShouldNotContain("nextPage");
    }

    [Fact]
    public void CreatePagedResponse_WithoutTotalCount_SetsCorrectMetadata()
    {
        // Arrange
        var data = new List<string> { "item1", "item2", "item3" };
        var parameters = new PaginationQuery(1) { PageSize = 10 };
        var baseUri = "https://api.example.com";
        var route = "/users";

        // Act
        var result = PaginationHelper.CreatePagedResponse(data, parameters, baseUri, route);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Metadata.CurrentPage.ShouldBe(1);
        result.Metadata.PageSize.ShouldBe(10);
        result.Metadata.TotalCount.ShouldBeNull();
        result.Metadata.TotalPages.ShouldBeNull();
        result.Metadata.HasPrevious.ShouldBeFalse();
        result.Metadata.HasNext.ShouldBeFalse();
        
        // Check links
        result.Metadata.Links.Keys.ShouldContain("firstPage");
        result.Metadata.Links.Keys.ShouldNotContain("lastPage");
        result.Metadata.Links.Keys.ShouldNotContain("previousPage");
        result.Metadata.Links.Keys.ShouldNotContain("nextPage");
    }

    [Fact]
    public void PaginationQuery_Constructor_SetsCorrectDefaults()
    {
        // Act
        var query = new PaginationQuery(3);

        // Assert
        query.PageNumber.ShouldBe(3);
        query.PageSize.ShouldBe(10); // Assuming default page size is 10
    }

    [Fact]
    public void PaginationQuery_CustomPageSize_SetsCorrectly()
    {
        // Act
        var query = new PaginationQuery(2) { PageSize = 25 };

        // Assert
        query.PageNumber.ShouldBe(2);
        query.PageSize.ShouldBe(25);
    }

    [Fact]
    public void PaginationMetadata_Constructor_CalculatesCorrectValues()
    {
        // Act
        var metadata = new PaginationMetadata(2, 10, 25);

        // Assert
        metadata.CurrentPage.ShouldBe(2);
        metadata.PageSize.ShouldBe(10);
        metadata.TotalCount.ShouldBe(25);
        metadata.TotalPages.ShouldBe(3);
        metadata.HasPrevious.ShouldBeTrue();
        metadata.HasNext.ShouldBeTrue();
    }

    [Fact]
    public void PaginationMetadata_FirstPage_HasNoPrevious()
    {
        // Act
        var metadata = new PaginationMetadata(1, 10, 25);

        // Assert
        metadata.HasPrevious.ShouldBeFalse();
        metadata.HasNext.ShouldBeTrue();
    }

    [Fact]
    public void PaginationMetadata_LastPage_HasNoNext()
    {
        // Act
        var metadata = new PaginationMetadata(3, 10, 25);

        // Assert
        metadata.HasPrevious.ShouldBeTrue();
        metadata.HasNext.ShouldBeFalse();
    }

    [Fact]
    public void PaginationMetadata_WithoutTotalCount_ReturnsNullValues()
    {
        // Act
        var metadata = new PaginationMetadata(1, 10, null);

        // Assert
        metadata.CurrentPage.ShouldBe(1);
        metadata.PageSize.ShouldBe(10);
        metadata.TotalCount.ShouldBeNull();
        metadata.TotalPages.ShouldBeNull();
        metadata.HasPrevious.ShouldBeFalse();
        metadata.HasNext.ShouldBeFalse();
    }

    [Fact]
    public void PagedResponse_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var items = new List<string> { "item1", "item2" };
        var metadata = new PaginationMetadata(1, 10, 20);

        // Act
        var response = new PagedResponse<string>
        {
            Items = items,
            Metadata = metadata
        };

        // Assert
        response.Items.ShouldBe(items);
        response.Metadata.ShouldBe(metadata);
    }

    [Fact]
    public void PagedNextPageResponse_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var items = new List<string> { "item1", "item2" };
        var metadata = new PaginationNextPageMetaddata(10, "token123");

        // Act
        var response = new PagedNextPageResponse<string>
        {
            Items = items,
            Metadata = metadata
        };

        // Assert
        response.Items.ShouldBe(items);
        response.Metadata.ShouldBe(metadata);
        response.Metadata.NextPageToken.ShouldBe("token123");
    }
}