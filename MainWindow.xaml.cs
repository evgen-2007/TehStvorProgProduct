using System;
using System.Data;
using System.IO;
using System.Windows;
using MySql.Data.MySqlClient;

namespace MyDentalApp
{
    public partial class MainWindow : Window
    {
        // Рядок підключення
        string connString = "Server=localhost;Database=dental_db;Uid=root;Pwd=evgen1128BARON;Charset=utf8mb4;";
        DataTable dt = new DataTable();

        public MainWindow()
        {
            InitializeComponent();
        }

        // Логіка входу
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Прибрали помилковий catch, який стояв тут без try
            if (UserPasswordBox.Password == "1234") 
            {
                LoginPanel.Visibility = Visibility.Collapsed;
                MainControl.Visibility = Visibility.Visible;
                LoadData();
            }
            else
            {
                MessageBox.Show("Невірний пароль!");
            }
        }

        // Завантаження даних
        private void LoadData()
        {
            try
            {
                using var conn = new MySqlConnection(connString);
                conn.Open();
                // Гарантуємо, що база віддає дані в UTF8
                new MySqlCommand("SET NAMES utf8mb4;", conn).ExecuteNonQuery();

                var adapter = new MySqlDataAdapter("SELECT * FROM patients", conn);
                dt.Clear();
                adapter.Fill(dt);
                PatientsGrid.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex) { MessageBox.Show("Помилка БД: " + ex.Message); }
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e) => LoadData();

        // Додавання пацієнта
        private void AddPatient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new MySqlConnection(connString);
                conn.Open();
                new MySqlCommand("SET NAMES utf8mb4;", conn).ExecuteNonQuery();

                string cmdText = "INSERT INTO patients (lastName, gender, age, city, diagnosis) VALUES (@ln, @gn, @ag, @ct, @dg)";
                var cmd = new MySqlCommand(cmdText, conn);
                cmd.Parameters.AddWithValue("@ln", TxtLastName.Text);
                cmd.Parameters.AddWithValue("@gn", TxtGender.Text);
                cmd.Parameters.AddWithValue("@ag", TxtAge.Text);
                cmd.Parameters.AddWithValue("@ct", TxtCity.Text);
                cmd.Parameters.AddWithValue("@dg", TxtDiagnosis.Text);

                cmd.ExecuteNonQuery();
                LoadData();
                
                // Очищення полів
                TxtLastName.Clear(); TxtAge.Clear(); TxtDiagnosis.Clear();
                TxtGender.Clear(); TxtCity.Clear();
            }
            catch (Exception ex) { MessageBox.Show("Помилка додавання: " + ex.Message); }
        }

        // ГЕНЕРАЦІЯ ЗВІТУ
        private void GenerateWordReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filterDiag = DiagFilterInput.Text.Trim();
                string minAgeStr = AgeLimitInput.Text.Trim();

                if (string.IsNullOrEmpty(filterDiag))
                {
                    MessageBox.Show("Будь ласка, введіть діагноз у поле підказки!");
                    return;
                }

                int.TryParse(minAgeStr, out int minAge);

                using var conn = new MySqlConnection(connString);
                conn.Open();
                new MySqlCommand("SET NAMES utf8mb4;", conn).ExecuteNonQuery();

                var cmd = new MySqlCommand("SELECT * FROM patients WHERE diagnosis = @diag AND age >= @age", conn);
                cmd.Parameters.AddWithValue("@diag", filterDiag);
                cmd.Parameters.AddWithValue("@age", minAge);

                var adapter = new MySqlDataAdapter(cmd);
                var reportDt = new DataTable();
                adapter.Fill(reportDt);

                if (reportDt.Rows.Count == 0)
                {
                    MessageBox.Show("Пацієнтів за такими критеріями не знайдено.");
                    return;
                }

                // Виправлено назву змінної з 'path' на 'desktopPath' для StreamWriter
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dental_Report.txt");

                using (StreamWriter sw = new StreamWriter(desktopPath, false, System.Text.Encoding.UTF8))
                {
                    int nonResidentCount = 0; 

                    sw.WriteLine("==========================================");
                    sw.WriteLine($"ЗВІТ ЗА ДІАГНОЗОМ: {filterDiag.ToUpper()}");
                    sw.WriteLine($"Мінімальний вік: {minAge}");
                    sw.WriteLine($"Дата створення: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sw.WriteLine("==========================================");
                    sw.WriteLine(string.Format("{0,-20} | {1,-5} | {2,-15}", "Прізвище", "Вік", "Місто"));
                    sw.WriteLine("------------------------------------------");

                    foreach (DataRow row in reportDt.Rows)
                    {
                        // Виправляємо старі "?" на "і" прямо під час запису
                        string lastName = row["lastName"].ToString().Replace("?", "і");
                        string city = row["city"].ToString().Replace("?", "і");
                        int age = Convert.ToInt32(row["age"]);

                        // Рахуємо іногородніх (клініка в м. Суми)
                        if (city.Trim().ToLower() != "суми")
                        {
                            nonResidentCount++;
                        }

                        sw.WriteLine(string.Format("{0,-20} | {1,-5} | {2,-15}", lastName, age, city));
                    }

                    sw.WriteLine("------------------------------------------");
                    sw.WriteLine($"Всього пацієнтів у звіті: {reportDt.Rows.Count}");
                    sw.WriteLine($"З них іногородніх (не з м. Суми): {nonResidentCount}"); 
                    sw.WriteLine("------------------------------------------");
                }

                MessageBox.Show("Звіт збережено на Робочий стіл!");
            }
            catch (Exception ex) { MessageBox.Show("Помилка звіту: " + ex.Message); }
        }
    }
}