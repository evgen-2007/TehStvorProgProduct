using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MySql.Data.MySqlClient;

namespace MyDentalApp
{
    public partial class MainWindow : Window
    {
        // Реквізити підключення до MariaDB (Порт 3307)
        private string connectionString = "Server=localhost;Port=3307;Database=dental_bd;Uid=root;Pwd=;";

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Навігація та Вхід
        private void ShowRegisterPanel_Click(object sender, MouseButtonEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
        }

        private void ShowLoginPanel_Click(object sender, MouseButtonEventArgs e)
        {
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    // Використовуємо екранування назв для уникнення помилок синтаксису
                    string query = "SELECT count(*) FROM users WHERE `Прізвище` = @sur AND `Пароль` = @pass";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@sur", UserLoginBox.Text);
                    cmd.Parameters.AddWithValue("@pass", UserPasswordBox.Password);

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count > 0) {
                        LoginPanel.Visibility = Visibility.Collapsed;
                        MainControl.Visibility = Visibility.Visible;
                        LoadData(); 
                    } else MessageBox.Show("Доступ заборонено.");
                }
            } catch (Exception ex) { MessageBox.Show("Помилка входу: " + ex.Message); }
        }
        #endregion

        #region Управління працівниками
        private void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    // Зворотні лапки навколо `Ім'я` вирішують проблему з апострофом
                    string query = "INSERT INTO users (`Прізвище`, `Ім'я`, `Посада`, `Пароль`) VALUES (@s, @n, @p, @pw)";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@s", RegSur.Text);
                    cmd.Parameters.AddWithValue("@n", RegName.Text);
                    cmd.Parameters.AddWithValue("@p", RegPos.Text);
                    cmd.Parameters.AddWithValue("@pw", RegPass.Password);
                    
                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Працівника зареєстровано!");
                    ShowLoginPanel_Click(null, null);
                }
            } catch (Exception ex) { MessageBox.Show("Помилка реєстрації: " + ex.Message); }
        }

        private void ClearUsers_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Ви впевнені, що хочете видалити всіх працівників?", "Увага", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes) {
                try {
                    using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                        conn.Open();
                        // Команда TRUNCATE очищує таблицю та скидає лічильник номерів
                        new MySqlCommand("TRUNCATE TABLE users", conn).ExecuteNonQuery();
                        MessageBox.Show("Базу працівників очищено. Повернення до входу.");
                        MainControl.Visibility = Visibility.Collapsed;
                        LoginPanel.Visibility = Visibility.Visible;
                    }
                } catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
        #endregion

        #region Робота з пацієнтами
        private void LoadData()
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    // Сортуємо за номером для коректного відображення в таблиці
                    MySqlDataAdapter adapter = new MySqlDataAdapter("SELECT * FROM patients ORDER BY `№` ASC", conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    PatientsGrid.ItemsSource = dt.DefaultView;
                }
            } catch (Exception ex) { MessageBox.Show("Помилка завантаження: " + ex.Message); }
        }

        private void AddPatient_Click(object sender, RoutedEventArgs e)
        {
            // 1. Перевірка на порожні поля
            if (string.IsNullOrWhiteSpace(TxtLastName.Text) || 
                string.IsNullOrWhiteSpace(TxtAge.Text) || 
                string.IsNullOrWhiteSpace(TxtCity.Text) || 
                string.IsNullOrWhiteSpace(TxtDiagnosis.Text) || 
                ComboGender.SelectedItem == null)
            {
                MessageBox.Show("Помилка: Усі поля повинні бути заповнені!", "Валідація", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Зупиняємо виконання методу, запит до БД не піде
            }

            // 2. Додаткова перевірка: чи є вік числом?
            if (!int.TryParse(TxtAge.Text, out int age) || age <= 0 || age > 120)
            {
                MessageBox.Show("Будь ласка, введіть коректний вік (число від 1 до 120).", "Помилка введення");
                return;
            }

            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query = "INSERT INTO patients (`Прізвище`, `Стать`, `Вік`, `Місто`, `Діагноз`) VALUES (@ln, @gn, @ag, @ct, @dg)";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@ln", TxtLastName.Text.Trim());
                    cmd.Parameters.AddWithValue("@gn", (ComboGender.SelectedItem as ComboBoxItem)?.Content.ToString());
                    cmd.Parameters.AddWithValue("@ag", age);
                    cmd.Parameters.AddWithValue("@ct", TxtCity.Text.Trim());
                    cmd.Parameters.AddWithValue("@dg", TxtDiagnosis.Text.Trim());
                    
                    cmd.ExecuteNonQuery();

                    // Очищення полів після успішного додавання
                    TxtLastName.Clear(); TxtAge.Clear(); TxtCity.Clear(); TxtDiagnosis.Clear();
                    ComboGender.SelectedIndex = -1; 
                    
                    LoadData(); // Оновлюємо таблицю
                    MessageBox.Show("Пацієнта успішно додано.");
                }
            } catch (Exception ex) { MessageBox.Show("Помилка БД: " + ex.Message); }
        }
        #endregion

        #region Звіти
        private void GenerateTxtReport_Click(object sender, RoutedEventArgs e)
        {
            try {
                string fileName = $"Звіт_Пацієнти_{DateTime.Now:yyyyMMdd_HHmm}.txt";
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                int.TryParse(AgeLimitInput.Text, out int limitAge);
                string diagFilter = DiagFilterInput.Text;

                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query = "SELECT * FROM patients WHERE `Вік` >= @age AND `Діагноз` LIKE @diag";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@age", limitAge);
                    cmd.Parameters.AddWithValue("@diag", "%" + diagFilter + "%");

                    DataTable dt = new DataTable();
                    using (MySqlDataReader rdr = cmd.ExecuteReader()) { dt.Load(rdr); }

                    // Обчислення динамічної ширини колонок
                    int maxSur = Math.Max("Прізвище".Length, dt.AsEnumerable().Select(r => r["Прізвище"].ToString().Length).DefaultIfEmpty(0).Max());
                    int maxCity = Math.Max("Місто".Length, dt.AsEnumerable().Select(r => r["Місто"].ToString().Length).DefaultIfEmpty(0).Max());
                    int maxDiag = Math.Max("Діагноз".Length, dt.AsEnumerable().Select(r => r["Діагноз"].ToString().Length).DefaultIfEmpty(0).Max());

                    using (StreamWriter sw = new StreamWriter(path)) {
                        sw.WriteLine("           ЗВІТ СТОМАТОЛОГІЧНОЇ КЛІНІКИ");
                        sw.WriteLine("================================================================");
                        sw.WriteLine($"Критерії: Вік >= {limitAge} | Діагноз: {(string.IsNullOrEmpty(diagFilter) ? "Всі" : diagFilter)}");
                        sw.WriteLine("----------------------------------------------------------------");

                        // Формат рядка з авто-шириною
                        string format = "{0,-3} | {1,-" + maxSur + "} | {2,-2} | {3,-3} | {4,-" + maxCity + "} | {5}";
                        
                        sw.WriteLine(string.Format(format, "№", "Прізвище", "Ст", "Вік", "Місто", "Діагноз"));
                        sw.WriteLine(new string('-', maxSur + maxCity + maxDiag + 25));

                        int countOthers = 0;
                        foreach (DataRow row in dt.Rows) {
                            if (row["Місто"].ToString().Trim().ToLower() != "суми") countOthers++;
                            sw.WriteLine(string.Format(format, row["№"], row["Прізвище"], row["Стать"], row["Вік"], row["Місто"], row["Діагноз"]));
                        }
                        
                        sw.WriteLine("================================================================");
                        sw.WriteLine($"Іногородніх: {countOthers}");
                    }
                }
                MessageBox.Show($"Звіт збережено: {fileName}");
            } catch (Exception ex) { MessageBox.Show("Помилка звіту: " + ex.Message); }
        }
        #endregion

        private void RefreshData_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}