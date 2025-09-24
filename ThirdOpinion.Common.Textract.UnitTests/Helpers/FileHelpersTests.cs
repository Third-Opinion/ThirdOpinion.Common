using FluentAssertions;
using ThirdOpinion.Common.Textract.Helpers;

namespace TextractLib.Tests.Helpers;

public class FileHelpersTests
{
    [Fact]
    public void TestFileInformationExtraction()
    {
        var filesToTextract = new List<string>
        {
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg3.tif",
            "nani/Jane-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg5.pdf",
            "nani/Bahena Smith-Omar-19760803~459296~20200715~70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9~pg1.pdf",
            "nani/Guzman Jones-Adulfo-19630917~408812~20190729~3DEFEA09-CD11-43E1-B33E-5FF77E491BFD~pg3.tif"
        };

        var fileInformation = FileInformation.ExtractFileElementsFromDocument(filesToTextract[0]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Ken-Sheppard");
        fileInformation.DOB.Should().Be("19450314");
        fileInformation.PageStart.Should().Be("1");
        fileInformation.Id.Should().Be("19450314~332056~20161216");
        fileInformation.Guid.Should().Be("240B2BA8-C1EC-48BE-AE86-48461FCF0D93");
        fileInformation.PageStart.Should().Be("1");

        fileInformation = FileInformation.ExtractFileElementsFromDocument(filesToTextract[1]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Ken-Sheppard");
        fileInformation.DOB.Should().Be("19450314");
        fileInformation.Id.Should().Be("19450314~332056~20161216");
        fileInformation.Guid.Should().Be("240B2BA8-C1EC-48BE-AE86-48461FCF0D93");
        fileInformation.PageStart.Should().Be("2");

        fileInformation = FileInformation.ExtractFileElementsFromDocument(filesToTextract[3]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Jane-Althea");
        fileInformation.DOB.Should().Be("19601023");
        fileInformation.Id.Should().Be("19601023~45750~20160226");
        fileInformation.Guid.Should().Be("744C6564-BD38-4E65-AC68-124636C46D21");
        fileInformation.PageStart.Should().Be("5");

        fileInformation = FileInformation.ExtractFileElementsFromDocument(filesToTextract[4]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Bahena Smith-Omar");
        fileInformation.DOB.Should().Be("19760803");
        fileInformation.Id.Should().Be("19760803~459296~20200715");
        fileInformation.Guid.Should().Be("70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9");
        fileInformation.PageStart.Should().Be("1");

        fileInformation = FileInformation.ExtractFileElementsFromDocument(filesToTextract[5]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Guzman Jones-Adulfo");
        fileInformation.DOB.Should().Be("19630917");
        fileInformation.Id.Should().Be("19630917~408812~20190729");
        fileInformation.Guid.Should().Be("3DEFEA09-CD11-43E1-B33E-5FF77E491BFD");
        fileInformation.PageStart.Should().Be("3");
    }

    [Fact]
    public void TestFileInformationExtractionComparison()
    {
        var filesToTextract = new List<string>
        {
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg3.tif",
            "nani/Jane-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg5.pdf",
            "nani/Bahena Smith-Omar-19760803~459296~20200715~70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9~pg1.pdf",
            "nani/Guzman Jones-Adulfo-19630917~408812~20190729~3DEFEA09-CD11-43E1-B33E-5FF77E491BFD~pg3.tif"
        };

        var fileComparer = new FileInformationComparer();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromDocument(filesToTextract[0]),
            FileInformation.ExtractFileElementsFromDocument(filesToTextract[1])).Should().BeTrue();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromDocument(filesToTextract[1]),
            FileInformation.ExtractFileElementsFromDocument(filesToTextract[1])).Should().BeTrue();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromDocument(filesToTextract[0]),
            FileInformation.ExtractFileElementsFromDocument(filesToTextract[0])).Should().BeTrue();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromDocument(filesToTextract[3]),
            FileInformation.ExtractFileElementsFromDocument(filesToTextract[4])).Should().BeFalse();
    }

    [Fact]
    public void TestExtractFileElementsFromTextractJsonCompare()
    {
        var textractJsonFiles = new[]
        {
            "nani/Afflick-Blthea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg2.pdf-textract-250328135739.json",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif-textract-250328135740.json",
            "nani/Bahena Smith-Omar-19760803~459296~20200715~70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9~pg1.pdf-textract-250328135741.json",
            "nani/Bahena Smith-Omar-19760803~459296~20200715~70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9~pg2.pdf-textract-250328135741.json"
        };

        var fileComparer = new FileInformationComparer();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[0]),
            FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[0])).Should().BeTrue();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[2]),
            FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[3])).Should().BeTrue();

        fileComparer.Equals(FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[0]),
            FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[1])).Should().BeFalse();
    }

    [Fact]
    public void TestExtractFileElementsFromTextractJson()
    {
        var textractJsonFiles = new[]
        {
            "nani/Afflick-Blthea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg2.pdf-textract-250328135739.json",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif-textract-250328135740.json",
            "nani/Bahena Smith-Omar-19760803~459296~20200715~70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9~pg1.pdf-textract-250328135741.json"
        };

        var fileInformation = FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[0]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Afflick-Blthea");
        fileInformation.DOB.Should().Be("19601023");
        fileInformation.Id.Should().Be("19601023~45750~20160226");
        fileInformation.Guid.Should().Be("744C6564-BD38-4E65-AC68-124636C46D21");
        fileInformation.PageStart.Should().Be("2");
        fileInformation.KeyFull.Should().Be(textractJsonFiles[0]);

        fileInformation = FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[1]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Ken-Sheppard");
        fileInformation.DOB.Should().Be("19450314");
        fileInformation.Id.Should().Be("19450314~332056~20161216");
        fileInformation.Guid.Should().Be("240B2BA8-C1EC-48BE-AE86-48461FCF0D93");
        fileInformation.PageStart.Should().Be("1");
        fileInformation.KeyFull.Should().Be(textractJsonFiles[1]);

        fileInformation = FileInformation.ExtractFileElementsFromTextractJson(textractJsonFiles[2]);
        fileInformation.Should().NotBeNull();
        fileInformation.Name.Should().Be("Bahena Smith-Omar");
        fileInformation.DOB.Should().Be("19760803");
        fileInformation.Id.Should().Be("19760803~459296~20200715");
        fileInformation.Guid.Should().Be("70B08DF4-C9F7-4C61-80F1-DBA2B7936AE9");
        fileInformation.PageStart.Should().Be("1");
        fileInformation.KeyFull.Should().Be(textractJsonFiles[2]);
    }

    [Fact]
    public void TestMergeSinglePatient()
    {
        var filesToTextract = new List<string>
        {
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg3.tif"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);
        Assert.Equal(1, mergedFiles.Count);
        Assert.Equal(3, mergedFiles[0].Length);

        var fileComparer = new FileInformationComparer();
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][1]));
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][2]));
    }

    [Fact]
    public void TestMergeTextractSinglePatient()
    {
        var filesToTextract = new List<string>
        {
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg1.pdf-textract-250328135739.json",
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg3.pdf-textract-250328135739.json",
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg3.pdf-textract-250328135739.json"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);
        Assert.Equal(1, mergedFiles.Count);
        Assert.Equal(3, mergedFiles[0].Length);

        var fileComparer = new FileInformationComparer();
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][1]));
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][2]));
    }

    [Fact]
    public void TestMergeThreePatients()
    {
        var filesToTextract = new List<string>
        {
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg3.tif",
            "Jon-Sheppard-19450314~332056~20161216~240B2BA8-D1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "Joe-Sheppard-99450314~332056~20161216~240B2BA8-D1EC-48BE-AE86-48461FCF0D93~pg1.pdf"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);

        Assert.Equal(3, mergedFiles.Count);
        Assert.Equal(3, mergedFiles[0].Length);
        Assert.Equal(1, mergedFiles[1].Length);
        Assert.Equal(1, mergedFiles[2].Length);

        var fileComparer = new FileInformationComparer();
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][1]));
        Assert.True(fileComparer.Equals(mergedFiles[0][1], mergedFiles[0][2]));
    }

    [Fact]
    public void TestMergeTexttractThreePatients()
    {
        var filesToTextract = new List<string>
        {
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg1.pdf-textract-250328135739.json",
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg3.pdf-textract-250328135739.json",
            "nani/Afflick-Althea-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg2.pdf-textract-250328135739.json",
            "nani/Joe-Sheppard-19601023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg1.pdf-textract-250328135739.json",
            "nani/Joe-Sheppard-20001023~45750~20160226~744C6564-BD38-4E65-AC68-124636C46D21~pg1.pdf-textract-250328135739.json"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);

        Assert.Equal(3, mergedFiles.Count);
        Assert.Equal(3, mergedFiles[0].Length);
        Assert.Equal(1, mergedFiles[1].Length);
        Assert.Equal(1, mergedFiles[2].Length);

        var fileComparer = new FileInformationComparer();
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][1]));
        Assert.True(fileComparer.Equals(mergedFiles[0][1], mergedFiles[0][2]));
    }

    [Fact]
    public void TestMergeThreePatientsDifferentGuids()
    {
        var filesToTextract = new List<string>
        {
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-FFFF-48461FCF0D93~pg1.tif",
            "Jon-Sheppard-19450314~332056~20161216~240B2BA8-D1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "Joe-Sheppard-99450314~332056~20161216~240B2BA8-D1EC-48BE-AE86-48461FCF0D93~pg1.pdf"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);

        Assert.Equal(4, mergedFiles.Count);
        Assert.Equal(2, mergedFiles[0].Length);
        Assert.Equal(1, mergedFiles[1].Length);
        Assert.Equal(1, mergedFiles[2].Length);
        Assert.Equal(1, mergedFiles[3].Length);

        var fileComparer = new FileInformationComparer();
        Assert.True(fileComparer.Equals(mergedFiles[0][0], mergedFiles[0][1]));
    }

    [Fact]
    public void TestMergeMultiplePages()
    {
        var filesToTextract = new List<string>
        {
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg1.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg2.tif",
            "nani/Ken-Sheppard-19450314~332056~20161216~240B2BA8-C1EC-48BE-AE86-48461FCF0D93~pg3.tif"
        };
        var mergedFiles = FileHelpers.GroupDocuments(filesToTextract);
        Assert.Equal(1, mergedFiles.Count);
        Assert.Equal(3, mergedFiles[0].Length);
    }
}