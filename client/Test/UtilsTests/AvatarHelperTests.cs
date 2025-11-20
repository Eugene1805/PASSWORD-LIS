using PASSWORD_LIS_Client.Utils;
using System;
using Xunit;

namespace Test.UtilsTests
{
    public class AvatarHelperTests
    {
        public AvatarHelperTests()
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);
            }
        }

        [Theory]
        [InlineData(1, "Avatar1.png")]
        [InlineData(2, "Avatar2.png")]
        [InlineData(3, "Avatar3.png")]
        [InlineData(4, "Avatar4.png")]
        [InlineData(5, "Avatar5.png")]
        [InlineData(6, "Avatar6.png")]
        public void GetAvatarUriById_WithValidPhotoId_ShouldReturnCorrectUri(int photoId, string expectedFileName)
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(photoId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(expectedFileName, result.ToString());
            Assert.Contains("/Resources/", result.ToString());
            Assert.Contains("pack://application:,,,", result.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(100)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void GetAvatarUriById_WithInvalidPhotoId_ShouldReturnDefaultUri(int photoId)
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(photoId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AvatarDefault.png", result.ToString());
            Assert.Contains("/Resources/", result.ToString());
            Assert.Contains("pack://application:,,,", result.ToString());
        }

        [Fact]
        public void GetAvatarUriById_WithPhotoIdAtLowerBoundary_ShouldReturnAvatar1()
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(1);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Avatar1.png", result.ToString());
        }

        [Fact]
        public void GetAvatarUriById_WithPhotoIdAtUpperBoundary_ShouldReturnAvatar6()
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(6);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Avatar6.png", result.ToString());
        }

        [Fact]
        public void GetAvatarUriById_WithPhotoIdJustBelowLowerBoundary_ShouldReturnDefault()
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(0);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AvatarDefault.png", result.ToString());
        }

        [Fact]
        public void GetAvatarUriById_WithPhotoIdJustAboveUpperBoundary_ShouldReturnDefault()
        {
            // Act
            var result = AvatarHelper.GetAvatarUriById(7);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("AvatarDefault.png", result.ToString());
        }
    }
}
