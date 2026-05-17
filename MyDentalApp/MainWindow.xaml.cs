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
        private string connectionString = "Server=localhost;Port=3307;Database=dental_bd;Uid=root;Pwd=;";
        private string currentUserLastName = string.Empty;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка інсталізації інтерфейсу:\n{ex.Message}\n\nВнутрішні виключення: {ex.InnerException?.Message}", "Критический сбой");
                Environment.Exit(0);
            }
        }

        #region Навігація і Вхід
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
                    string query = "SELECT count(*) FROM users WHERE `Прізвище` = @sur AND `Пароль` = @pass";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@sur", UserLoginBox.Text.Trim());
                    cmd.Parameters.AddWithValue("@pass", UserPasswordBox.Password);

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count > 0) {
                        currentUserLastName = UserLoginBox.Text.Trim();

                        LoginPanel.Visibility = Visibility.Collapsed;
                        MainControl.Visibility = Visibility.Visible;
                        LoadData(); 
                    } else MessageBox.Show("Доступ заборонено");
                }
            } catch (Exception ex) { MessageBox.Show("Помилка входу: " + ex.Message); }
        }
        #endregion

        #region Видалення персоналу
        private void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query = "INSERT INTO users (`Прізвище`, `Ім'я`, `Посада`, `Пароль`) VALUES (@s, @n, @p, @pw)";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@s", RegSur.Text.Trim());
                    cmd.Parameters.AddWithValue("@n", RegName.Text.Trim());
                    cmd.Parameters.AddWithValue("@p", RegPos.Text.Trim());
                    cmd.Parameters.AddWithValue("@pw", RegPass.Password);
                    
                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Співробітник зареєстрований!");
                    
                    ShowLoginPanel_Click(this, new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
                }
            } catch (Exception ex) { MessageBox.Show("Помилка реєстрації: " + ex.Message); }
        }

        private void ClearUsers_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Ви впевнені, що хочете видалити всіх співробітників?", "УВАГА!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes) {
                try {
                    using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                        conn.Open();
                        new MySqlCommand("TRUNCATE TABLE users", conn).ExecuteNonQuery();
                        MessageBox.Show("База співробітників очищена. Перехід до вікна входу.");
                        MainControl.Visibility = Visibility.Collapsed;
                        LoginPanel.Visibility = Visibility.Visible;
                        currentUserLastName = string.Empty;
                    }
                } catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
        #endregion

        #region Работа с пацієнтами
        private void LoadData()
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    MySqlDataAdapter adapter = new MySqlDataAdapter("SELECT * FROM patients ORDER BY `№` ASC", conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    PatientsGrid.ItemsSource = dt.DefaultView;
                }
            } catch (Exception ex) { MessageBox.Show("Помилка загрузки: " + ex.Message); }
        }

        private void AddOrUpdatePatient_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtLastName.Text) || 
                string.IsNullOrWhiteSpace(TxtAge.Text) || 
                string.IsNullOrWhiteSpace(TxtCity.Text) || 
                string.IsNullOrWhiteSpace(TxtDiagnosis.Text) || 
                ComboGender.SelectedItem == null)
            {
                MessageBox.Show("Помилка: Всі поля мають бути заповнені!", "Валідація", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtAge.Text, out int age) || age <= 0 || age > 120)
            {
                MessageBox.Show("Будь ласка, ведіть коректний вік (від 1 до 120)", "Помилка вводу");
                return;
            }

            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query;

                    if (!string.IsNullOrEmpty(TxtPatientId.Text)) {
                        query = "UPDATE patients SET `Прізвище`=@ln, `Стать`=@gn, `Вік`=@ag, `Місто`=@ct, `Діагноз`=@dg WHERE `№`=@id";
                    } else {
                        query = "INSERT INTO patients (`Прізвище`, `Стать`, `Вік`, `Місто`, `Діагноз`) VALUES (@ln, @gn, @ag, @ct, @dg)";
                    }

                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@ln", TxtLastName.Text.Trim());
                    cmd.Parameters.AddWithValue("@gn", (ComboGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ч");
                    cmd.Parameters.AddWithValue("@ag", age);
                    cmd.Parameters.AddWithValue("@ct", TxtCity.Text.Trim());
                    cmd.Parameters.AddWithValue("@dg", TxtDiagnosis.Text.Trim());

                    if (!string.IsNullOrEmpty(TxtPatientId.Text)) {
                        cmd.Parameters.AddWithValue("@id", Convert.ToInt32(TxtPatientId.Text));
                    }
                    
                    cmd.ExecuteNonQuery();
                    MessageBox.Show(string.IsNullOrEmpty(TxtPatientId.Text) ? "Пацієнт успішно доданий" : "Данні пацієнта оновлені");
                    
                    ClearPatientForm();
                    LoadData();
                }
            } catch (Exception ex) { MessageBox.Show("Помилка БД: " + ex.Message); }
        }

        private void SearchPatient_Click(object sender, RoutedEventArgs e)
        {
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query = "SELECT * FROM patients WHERE `Прізвище` LIKE @search ORDER BY `№` ASC";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@search", TxtSearchLastName.Text.Trim() + "%");

                    MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    PatientsGrid.ItemsSource = dt.DefaultView;
                }
            } catch (Exception ex) { MessageBox.Show("Помилка пошуку: " + ex.Message); }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchLastName.Clear();
            LoadData();
        }

        private void PatientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentUserLastName)) return;

            if (PatientsGrid.SelectedItem is DataRowView row)
            {
                Window passwordWindow = new Window
                {
                    Title = "Підтвердження доступу",
                    Width = 350, Height = 160,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false
                };

                StackPanel sp = new StackPanel { Margin = new Thickness(15) };
                TextBlock lbl = new TextBlock { Text = $"Введіть пароль для підтвердження ({currentUserLastName}):", Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold };
                PasswordBox pb = new PasswordBox { Height = 28, Margin = new Thickness(0, 0, 0, 15) };
                Button btnConfirm = new Button { Content = "Підтвердіть", Height = 28, IsDefault = true, Background = System.Windows.Media.Brushes.DarkGreen, Foreground = System.Windows.Media.Brushes.White };

                sp.Children.Add(lbl);
                sp.Children.Add(pb);
                sp.Children.Add(btnConfirm);
                passwordWindow.Content = sp;

                string enteredPassword = string.Empty;
                btnConfirm.Click += (s, ae) => {
                    enteredPassword = pb.Password;
                    passwordWindow.DialogResult = true;
                };

                if (passwordWindow.ShowDialog() == true)
                {
                    if (!VerifyUserPassword(currentUserLastName, enteredPassword))
                    {
                        MessageBox.Show("Невірний пароль! Доступ до даних заблоковано.", "Помилка безпеки", MessageBoxButton.OK, MessageBoxImage.Stop);
                        
                        PatientsGrid.SelectionChanged -= PatientsGrid_SelectionChanged;
                        PatientsGrid.SelectedItem = null;
                        PatientsGrid.SelectionChanged += PatientsGrid_SelectionChanged;
                        return;
                    }

                    TxtPatientId.Text = row["№"]?.ToString() ?? string.Empty;
                    TxtLastName.Text = row["Прізвище"]?.ToString() ?? string.Empty;
                    TxtAge.Text = row["Вік"]?.ToString() ?? string.Empty;
                    TxtCity.Text = row["Місто"]?.ToString() ?? string.Empty;
                    TxtDiagnosis.Text = row["Діагноз"]?.ToString() ?? string.Empty;

                    string gender = row["Стать"]?.ToString() ?? "Ч";
                    foreach (ComboBoxItem item in ComboGender.Items) {
                        if (item.Content?.ToString() == gender) {
                            ComboGender.SelectedItem = item;
                            break;
                        }
                    }

                    GroupPatientForm.Header = "Редагувати данні пацієнта";
                    BtnSavePatient.Content = "Зберегти зміни";
                    BtnSavePatient.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255));
                    BtnCancelEdit.Visibility = Visibility.Visible;
                }
                else
                {
                    PatientsGrid.SelectionChanged -= PatientsGrid_SelectionChanged;
                    PatientsGrid.SelectedItem = null;
                    PatientsGrid.SelectionChanged += PatientsGrid_SelectionChanged;
                }
            }
        }

        private bool VerifyUserPassword(string surName, string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            try {
                using (MySqlConnection conn = new MySqlConnection(connectionString)) {
                    conn.Open();
                    string query = "SELECT count(*) FROM users WHERE `Прізвище` = @sur AND `Пароль` = @pass";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@sur", surName);
                    cmd.Parameters.AddWithValue("@pass", password);

                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            } catch { return false; }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearPatientForm();
        }

        private void ClearPatientForm()
        {
            TxtPatientId.Text = string.Empty;
            TxtLastName.Clear(); 
            TxtAge.Clear(); 
            TxtCity.Clear(); 
            TxtDiagnosis.Clear();
            ComboGender.SelectedIndex = 0;

            GroupPatientForm.Header = "Реєстрація нового пацієнта";
            BtnSavePatient.Content = "Додати пацієнта";
            BtnSavePatient.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69));
            BtnCancelEdit.Visibility = Visibility.Collapsed;
            
            PatientsGrid.SelectionChanged -= PatientsGrid_SelectionChanged;
            PatientsGrid.SelectedItem = null;
            PatientsGrid.SelectionChanged += PatientsGrid_SelectionChanged;
        }
        #endregion

        #region Отчеты
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

                    int maxSur = Math.Max("Прізвище".Length, dt.AsEnumerable().Select(r => r["Прізвище"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());
                    int maxCity = Math.Max("Місто".Length, dt.AsEnumerable().Select(r => r["Місто"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());
                    int maxDiag = Math.Max("Діагноз".Length, dt.AsEnumerable().Select(r => r["Діагноз"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());

                    using (StreamWriter sw = new StreamWriter(path)) {
                        sw.WriteLine("           ЗВІТ СТОМАТОЛОГІЧНОЇ КЛІНІКИ");
                        sw.WriteLine("================================================================");
                        sw.WriteLine($"Критерії: Вік >= {limitAge} | Діагноз: {(string.IsNullOrEmpty(diagFilter) ? "Все" : diagFilter)}");
                        sw.WriteLine("----------------------------------------------------------------");

                        string format = "{0,-3} | {1,-" + maxSur + "} | {2,-2} | {3,-3} | {4,-" + maxCity + "} | {5}";
                        
                        sw.WriteLine(string.Format(format, "№", "Прізвище", "Ст", "Вік", "Місто", "Діагноз"));
                        sw.WriteLine(new string('-', maxSur + maxCity + maxDiag + 25));

                        int countOthers = 0;
                        foreach (DataRow row in dt.Rows) {
                            if ((row["Місто"]?.ToString() ?? "").Trim().ToLower() != "суми") countOthers++;
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