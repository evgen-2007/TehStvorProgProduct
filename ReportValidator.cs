using System;

namespace MyDentalApp
{
    public class ReportValidator
    {
        public bool ValidateFilterData(string ageText, string selectedDiag, out int validAge, out string errorMessage)
        {
            validAge = 0;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ageText) || string.IsNullOrWhiteSpace(selectedDiag))
            {
                errorMessage = "Помилка: Не всі поля заповнені!\nБудь ласка, вкажіть фільтр віку та оберіть діагноз для формування звіту.";
                return false;
            }

            if (!int.TryParse(ageText, out validAge) || validAge < 0 || validAge > 120)
            {
                errorMessage = "Будь ласка, введіть коректне числове значення для віку (від 0 до 120).";
                return false;
            }

            return true;
        }
    }
}