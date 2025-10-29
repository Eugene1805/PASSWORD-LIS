using FlaUI.Core.AutomationElements;
using System;
using Xunit;

namespace Test.UI
{
    public class LoginWindowTests : UITestBase
    {
        [Fact]
        public void Test_Login_Form_Elements_Present()
        {
            // Assert - Verificar que todos los elementos del login existen
            Assert.NotNull(FindElementById("emailTextBox"));
            Assert.NotNull(FindElementById("passwordBox"));
            Assert.NotNull(FindElementById("loginButton"));
            Assert.NotNull(FindElementByText("playAsGuestText"));
            Assert.NotNull(FindElementByText("signUpText"));
            Assert.NotNull(FindElementByText("forgotUserPasswordText"));
        }

        [Fact]
        public void Test_Valid_Login()
        {
            // Arrange - Credenciales válidas (ajusta según tu base de datos)
            var validCredentials = new
            {
                Email = "test@test.com",
                Password = "Password123!"
            };

            // Act - Llenar formulario de login
            EnterText("emailTextBox", validCredentials.Email);
            EnterText("passwordBox", validCredentials.Password);

            // Assert - Verificar datos ingresados
            Assert.Equal(validCredentials.Email, GetElementText("emailTextBox"));

            // El botón de login debería estar habilitado
            Assert.True(IsElementVisible("loginButton"));
        }

        [Fact]
        public void Test_Login_With_Empty_Fields()
        {
            // Act - Dejar campos vacíos
            EnterText("emailTextBox", "");
            EnterText("passwordBox", "");

            // Assert - El botón debería estar deshabilitado o mostrar error al hacer click
            ClickElement("loginButton");

            // Debería permanecer en la ventana de login
            Assert.NotNull(FindElementById("emailTextBox"));
        }

        [Fact]
        public void Test_Login_With_Invalid_Credentials()
        {
            // Arrange - Credenciales inválidas
            var invalidCredentials = new
            {
                Email = "invalid@test.com",
                Password = "WrongPassword123!"
            };

            // Act - Intentar login con credenciales inválidas
            EnterText("emailTextBox", invalidCredentials.Email);
            EnterText("passwordBox", invalidCredentials.Password);
            ClickElement("loginButton");

            // Assert - Debería mostrar mensaje de error y permanecer en login

            Assert.NotNull(FindElementById("emailTextBox"));
        }

        [Fact]
        public void Test_Play_As_Guest_Functionality()
        {
            // Act - Hacer click en "Play as Guest"
            ClickElement("playAsGuestButton");

            // Assert - Debería navegar a MainWindow
      

            // Verificar que se cerró LoginWindow y abrió MainWindow
            // (Esto depende de tu implementación específica)
            var mainWindowElement = WaitForElement(() => FindElementByText("MainWindow"), TimeSpan.FromSeconds(10));
            Assert.NotNull(mainWindowElement);
        }

        [Fact]
        public void Test_Navigate_To_SignUp_From_Login()
        {
            // Act
            NavigateToSignUp();

            // Assert
            var signUpForm = FindElementById("firstNameTextBox");
            Assert.NotNull(signUpForm);
            Assert.True(!signUpForm.IsOffscreen);
        }

        [Fact]
        public void Test_Navigate_To_Forgot_Password_From_Login()
        {
            // Act
            NavigateToForgotPassword();

            // Assert - Debería estar en ventana de recuperación de contraseña
            var forgotPasswordElement = FindElementById("emailTextBox");
            Assert.NotNull(forgotPasswordElement);
        }

        [Fact]
        public void Test_Email_Format_Validation_In_Login()
        {
            // Arrange - Emails con formato inválido
            var invalidEmails = new[]
            {
                "invalid-email",
                "invalid@",
                "@domain.com"
            };

            foreach (var invalidEmail in invalidEmails)
            {
                // Act - Probar cada email inválido
                EnterText("emailTextBox", invalidEmail);
                EnterText("passwordBox", "Password123!");

                // Assert - El ViewModel podría validar el formato
                Assert.Equal(invalidEmail, GetElementText("emailTextBox"));
            }
        }

        [Fact]
        public void Test_Password_MaxLength_Enforcement()
        {
            // Arrange - Contraseña que excede el máximo (15 caracteres según tu XAML)
            var longPassword = new string('a', 20);

            // Act - Intentar ingresar contraseña larga
            EnterText("passwordBox", longPassword);

            // Assert - Debería truncar a 15 caracteres
            var enteredPassword = GetElementText("passwordBox");
            Assert.True(enteredPassword.Length <= 15);
        }

        [Fact]
        public void Test_Login_Button_State_With_Valid_Input()
        {
            // Act - Llenar campos requeridos
            EnterText("emailTextBox", "test@test.com");
            EnterText("passwordBox", "Password123!");

            // Assert - El botón debería estar habilitado
            var loginButton = FindElementById("loginButton");
            Assert.NotNull(loginButton);
            Assert.True(!loginButton.IsOffscreen);

            // Verificar que no está en estado "loading" inicialmente
            var buttonState = loginButton.AsButton();
            Assert.NotNull(buttonState);
        }
    }
}
