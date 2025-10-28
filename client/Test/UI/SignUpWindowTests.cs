using Xunit;

namespace Test.UI
{
    public class SignUpWindowTests : UITestBase
    {
        public SignUpWindowTests()
    {
        // Navegar a SignUp antes de cada test
        NavigateToSignUp();
    }

    [Fact]
    public void Test_SignUp_Form_All_Elements_Present()
    {
        // Assert - Verificar que todos los elementos del formulario existen
        Assert.NotNull(FindElementById("firstNameTextBox"));
        Assert.NotNull(FindElementById("lastNameTextBox"));
        Assert.NotNull(FindElementById("nicknameTextBox"));
        Assert.NotNull(FindElementById("emailTextBox"));
        Assert.NotNull(FindElementById("passwordBox"));
        Assert.NotNull(FindElementById("confirmPasswordBox"));
        Assert.NotNull(FindElementById("signUpButton"));
        Assert.NotNull(FindElementByText("termsAndConditionsText"));
        Assert.NotNull(FindElementByText("logInText"));
    }

    [Fact]
    public void Test_Complete_Valid_SignUp_Form()
    {
        // Arrange - Datos válidos para registro
        var validUser = new
        {
            FirstName = "Ana",
            LastName = "García",
            Nickname = "anagarcia2024",
            Email = "ana.garcia@test.com",
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!"
        };

        // Act - Llenar formulario completo
        EnterText("firstNameTextBox", validUser.FirstName);
        EnterText("lastNameTextBox", validUser.LastName);
        EnterText("nicknameTextBox", validUser.Nickname);
        EnterText("emailTextBox", validUser.Email);
        EnterText("passwordBox", validUser.Password);
        EnterText("confirmPasswordBox", validUser.ConfirmPassword);

        // Assert - Verificar que todos los datos se ingresaron correctamente
        Assert.Equal(validUser.FirstName, GetElementText("firstNameTextBox"));
        Assert.Equal(validUser.LastName, GetElementText("lastNameTextBox"));
        Assert.Equal(validUser.Nickname, GetElementText("nicknameTextBox"));
        Assert.Equal(validUser.Email, GetElementText("emailTextBox"));

        // El botón de registro debería estar disponible
        Assert.True(IsElementVisible("signUpButton"));
    }

    [Fact]
    public void Test_SignUp_With_Missing_Required_Fields()
    {
        // Probar cada campo requerido individualmente
        var requiredFields = new[]
        {
                "firstNameTextBox",
                "lastNameTextBox",
                "nicknameTextBox",
                "emailTextBox"
            };

        foreach (var field in requiredFields)
        {
            // Llenar todos los campos excepto uno
            EnterText("firstNameTextBox", field == "firstNameTextBox" ? "" : "Test");
            EnterText("lastNameTextBox", field == "lastNameTextBox" ? "" : "User");
            EnterText("nicknameTextBox", field == "nicknameTextBox" ? "" : "testuser");
            EnterText("emailTextBox", field == "emailTextBox" ? "" : "test@test.com");
            EnterText("passwordBox", "Password123!");
            EnterText("confirmPasswordBox", "Password123!");

            // Intentar registrar
            ClickElement("signUpButton");

            // Debería permanecer en SignUp (no debería registrar)
            Assert.NotNull(FindElementById("firstNameTextBox"));
        }
    }

    [Fact]
    public void Test_Password_Confirmation_Validation()
    {
        // Arrange
        EnterText("firstNameTextBox", "Carlos");
        EnterText("lastNameTextBox", "López");
        EnterText("nicknameTextBox", "carlosl");
        EnterText("emailTextBox", "carlos@test.com");

        // Act - Contraseñas no coinciden
        EnterText("passwordBox", "Password123!");
        EnterText("confirmPasswordBox", "DifferentPassword123!");

        ClickElement("signUpButton");

        // Assert - Debería mostrar error y permanecer en SignUp
        Assert.NotNull(FindElementById("firstNameTextBox"));
    }

    [Fact]
    public void Test_Email_Uniqueness_Validation()
    {
        // Arrange - Email que probablemente ya existe
        EnterText("firstNameTextBox", "Nuevo");
        EnterText("lastNameTextBox", "Usuario");
        EnterText("nicknameTextBox", "nuevousuario");
        EnterText("emailTextBox", "existing@test.com"); // Asumiendo que existe
        EnterText("passwordBox", "Password123!");
        EnterText("confirmPasswordBox", "Password123!");

        // Act
        ClickElement("signUpButton");

        // Assert - Debería mostrar error de email duplicado
        Assert.NotNull(FindElementById("firstNameTextBox"));
    }

    [Fact]
    public void Test_Nickname_Uniqueness_Validation()
    {
        // Arrange - Nickname que probablemente ya existe
        EnterText("firstNameTextBox", "Usuario");
        EnterText("lastNameTextBox", "Nuevo");
        EnterText("nicknameTextBox", "admin"); // Probablemente existe
        EnterText("emailTextBox", "unique@test.com");
        EnterText("passwordBox", "Password123!");
        EnterText("confirmPasswordBox", "Password123!");

        // Act
        ClickElement("signUpButton");

        // Assert - Debería mostrar error de nickname duplicado
        Assert.NotNull(FindElementById("firstNameTextBox"));
    }

    [Fact]
    public void Test_Return_To_Login_From_SignUp()
    {
        // Act
        NavigateBackToLogin();

        // Assert - Debería estar de vuelta en Login
        var loginButton = FindElementById("loginButton");
        Assert.NotNull(loginButton);
        Assert.True(!loginButton.IsOffscreen);
    }

    [Fact]
    public void Test_Terms_And_Conditions_Link()
    {
        // Act - Hacer click en términos y condiciones
        ClickHyperlinkByText("termsAndConditionsText");

        // Assert - Debería abrir ventana/modal de términos
        // (Depende de tu implementación)

        // Verificar que SignUp sigue abierto
        Assert.NotNull(FindElementById("firstNameTextBox"));
    }

    [Fact]
    public void Test_Password_Strength_Validation()
    {
        // Arrange - Contraseñas débiles
        var weakPasswords = new[]
        {
                "123",           // Muy corta
                "password",      // Sin números/mayúsculas
                "PASSWORD",      // Sin minúsculas/números
                "1234567890"     // Solo números
            };

        foreach (var weakPassword in weakPasswords)
        {
            // Llenar formulario con contraseña débil
            EnterText("firstNameTextBox", "Test");
            EnterText("lastNameTextBox", "User");
            EnterText("nicknameTextBox", "testuser");
            EnterText("emailTextBox", "test@test.com");
            EnterText("passwordBox", weakPassword);
            EnterText("confirmPasswordBox", weakPassword);

            // El sistema podría prevenir el registro
            ClickElement("signUpButton");

            // Debería permanecer en SignUp
            Assert.NotNull(FindElementById("firstNameTextBox"));
        }
    }

    [Fact]
    public void Test_Special_Characters_In_Form()
    {
        // Arrange - Datos con caracteres especiales
        var specialData = new
        {
            FirstName = "María-José",
            LastName = "O'Connor",
            Nickname = "user_123-ñ",
            Email = "test.ñ@dominio-es.com"
        };

        // Act
        EnterText("firstNameTextBox", specialData.FirstName);
        EnterText("lastNameTextBox", specialData.LastName);
        EnterText("nicknameTextBox", specialData.Nickname);
        EnterText("emailTextBox", specialData.Email);
        EnterText("passwordBox", "Password123!");
        EnterText("confirmPasswordBox", "Password123!");

        // Assert - Debería aceptar caracteres especiales
        Assert.Equal(specialData.FirstName, GetElementText("firstNameTextBox"));
        Assert.Equal(specialData.LastName, GetElementText("lastNameTextBox"));
        Assert.Equal(specialData.Nickname, GetElementText("nicknameTextBox"));
        Assert.Equal(specialData.Email, GetElementText("emailTextBox"));
    }
}
}
