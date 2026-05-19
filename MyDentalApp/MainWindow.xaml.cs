using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MySql.Data.MySqlClient;

namespace MyDentalApp
{
    public partial class MainWindow : Window
    {
        private readonly string connectionString = "Server=localhost;Port=3307;Database=dental_bd;Uid=root;Pwd=;";
        private string currentUserLastName = string.Empty;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                CheckAndAddAvatarColumn();
                LoadData();
                LoadUniqueDiagnoses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка ініціалізації інтерфейсу:\n{ex.Message}\n\nВнутрішнє виключення: {ex.InnerException?.Message}", "Критичний збій");
                Environment.Exit(0);
            }
        }

        private void CheckAndAddAvatarColumn()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string checkQuery = "SHOW COLUMNS FROM users LIKE 'Аватарка'";
                    using (MySqlCommand cmd = new MySqlCommand(checkQuery, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows) return; 
                        }
                    }

                    string alterQuery = "ALTER TABLE users ADD COLUMN `Аватарка` LONGBLOB NULL";
                    using (MySqlCommand cmdAlter = new MySqlCommand(alterQuery, conn))
                    {
                        cmdAlter.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка перевірки структури бази: {ex.Message}");
            }
        }

        #region НАВІГАЦІЯ І ВХІД ДЛЯ ПЕРСОНАЛУ

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
            string username = UserLoginBox.Text.Trim();
            string password = UserPasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Заповніть всі поля авторизації!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM users WHERE `Прізвище` = @sur AND `Пароль` = @pass";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@sur", username);
                        cmd.Parameters.AddWithValue("@pass", password);

                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count > 0)
                        {
                            currentUserLastName = username;

                            string infoQuery = "SELECT `Прізвище`, `Ім'я`, `Посада`, `Аватарка` FROM users WHERE `Прізвище` = @sur LIMIT 1";
                            using (MySqlCommand infoCmd = new MySqlCommand(infoQuery, conn))
                            {
                                infoCmd.Parameters.AddWithValue("@sur", username);
                                using (MySqlDataReader reader = infoCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        string sName = reader["Прізвище"]?.ToString() ?? "";
                                        string fName = reader["Ім'я"]?.ToString() ?? "";
                                        string position = reader["Посада"]?.ToString() ?? "";
                                        
                                        TxtUserFullName.Text = $"{sName} {fName}".Trim();
                                        TxtUserPosition.Text = string.IsNullOrEmpty(position) ? "Посада не вказана" : position;

                                        if (reader["Аватарка"] != DBNull.Value)
                                        {
                                            byte[] imgBytes = (byte[])reader["Аватарка"];
                                            UserAvatarCircle.Fill = new ImageBrush(BytesToImage(imgBytes)) { Stretch = Stretch.UniformToFill };
                                        }
                                        else
                                        {
                                            UserAvatarCircle.Fill = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/clinic.png"))) { Stretch = Stretch.UniformToFill };
                                        }
                                    }
                                }
                            }

                            LoginPanel.Visibility = Visibility.Collapsed;
                            MainControl.Visibility = Visibility.Visible;
                            LoadData();
                            LoadUniqueDiagnoses();
                        }
                        else
                        {
                            MessageBox.Show("Невірне прізвище або пароль працівника!", "Помилка доступу", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка підключення при авторизації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(currentUserLastName)) return;

            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Оберіть фото для аватарки",
                Filter = "Зображення (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    byte[] imgBytes = File.ReadAllBytes(ofd.FileName);

                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        string updateQuery = "UPDATE users SET `Аватарка` = @img WHERE `Прізвище` = @sur";
                        using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@img", imgBytes);
                            cmd.Parameters.AddWithValue("@sur", currentUserLastName);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    UserAvatarCircle.Fill = new ImageBrush(BytesToImage(imgBytes)) { Stretch = Stretch.UniformToFill };
                    MessageBox.Show("Аватарку успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося зберегти фото: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private BitmapImage BytesToImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            BitmapImage image = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                ms.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
            }
            image.Freeze(); 
            return image;
        }

        private void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            string surname = RegSur.Text.Trim();
            string name = RegName.Text.Trim();
            string position = RegPos.Text.Trim();
            string password = RegPass.Password.Trim();

            if (string.IsNullOrEmpty(surname) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Прізвище, Ім'я та Пароль обов'язкові до заповнення!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO users (`Прізвище`, `Ім'я`, `Посада`, `Пароль`) VALUES (@s, @n, @p, @pw)";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@s", surname);
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@p", position);
                        cmd.Parameters.AddWithValue("@pw", password);

                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Співробітник успішно зареєстрований!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                        RegSur.Clear(); RegName.Clear(); RegPos.Clear(); RegPass.Clear();
                        ShowLoginPanel_Click(this, new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Помилка реєстрації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            currentUserLastName = string.Empty;
            TxtUserFullName.Text = "Неавторизовано";
            TxtUserPosition.Text = string.Empty;
            UserAvatarCircle.Fill = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/clinic.png"))) { Stretch = Stretch.UniformToFill };
            
            UserLoginBox.Clear();
            UserPasswordBox.Clear();

            MainControl.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
        }

        private void ClearUsers_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Ви впевнені, що хочете видалити всіх співробітників?", "УВАГА!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        new MySqlCommand("TRUNCATE TABLE users", conn).ExecuteNonQuery();
                        MessageBox.Show("База співробітників очищена. Перехід до вікна входу.", "Успіх");
                        LogoutButton_Click(this, new RoutedEventArgs());
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Помилка видалення: {ex.Message}"); }
            }
        }

        #endregion

        #region РОБОТА З ПАЦІЄНТАМИ (CRUD)

        private void LoadData()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    MySqlDataAdapter adapter = new MySqlDataAdapter("SELECT * FROM patients ORDER BY `№` ASC", conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    PatientsGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch { }
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
                MessageBox.Show("Будь ласка, введіть коректний вік (від 1 до 120)", "Помилка вводу");
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = !string.IsNullOrEmpty(TxtPatientId.Text) 
                        ? "UPDATE patients SET `Прізвище`=@ln, `Стать`=@gn, `Вік`=@ag, `Місто`=@ct, `Діагноз`=@dg WHERE `№`=@id"
                        : "INSERT INTO patients (`Прізвище`, `Стать`, `Вік`, `Місто`, `Діагноз`) VALUES (@ln, @gn, @ag, @ct, @dg)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ln", TxtLastName.Text.Trim());
                        cmd.Parameters.AddWithValue("@gn", (ComboGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ч");
                        cmd.Parameters.AddWithValue("@ag", age);
                        cmd.Parameters.AddWithValue("@ct", TxtCity.Text.Trim());
                        cmd.Parameters.AddWithValue("@dg", TxtDiagnosis.Text.Trim());

                        if (!string.IsNullOrEmpty(TxtPatientId.Text))
                        {
                            cmd.Parameters.AddWithValue("@id", Convert.ToInt32(TxtPatientId.Text));
                        }

                        cmd.ExecuteNonQuery();
                        MessageBox.Show(string.IsNullOrEmpty(TxtPatientId.Text) ? "Пацієнт успішно доданий" : "Дані пацієнта оновлені", "Успіх");

                        ClearPatientForm();
                        LoadData();
                        LoadUniqueDiagnoses();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Помилка БД: " + ex.Message); }
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
                Button btnConfirm = new Button { Content = "Підтвердити", Height = 28, IsDefault = true, Background = System.Windows.Media.Brushes.DarkGreen, Foreground = System.Windows.Media.Brushes.White };

                sp.Children.Add(lbl); sp.Children.Add(pb); sp.Children.Add(btnConfirm);
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
                        ResetGridSelection();
                        return;
                    }

                    TxtPatientId.Text = row["№"]?.ToString() ?? string.Empty;
                    TxtLastName.Text = row["Прізвище"]?.ToString() ?? string.Empty;
                    TxtAge.Text = row["Вік"]?.ToString() ?? string.Empty;
                    TxtCity.Text = row["Місто"]?.ToString() ?? string.Empty;
                    TxtDiagnosis.Text = row["Діагноз"]?.ToString() ?? string.Empty;

                    string gender = row["Стать"]?.ToString() ?? "Ч";
                    foreach (ComboBoxItem item in ComboGender.Items)
                    {
                        if (item.Content?.ToString() == gender)
                        {
                            ComboGender.SelectedItem = item;
                            break;
                        }
                    }

                    GroupPatientForm.Header = "Редагувати дані пацієнта";
                    BtnSavePatient.Content = "Зберегти зміни";
                    BtnSavePatient.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255));
                    BtnCancelEdit.Visibility = Visibility.Visible;
                }
                else
                {
                    ResetGridSelection();
                }
            }
        }

        private bool VerifyUserPassword(string surName, string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM users WHERE `Прізвище` = @sur AND `Пароль` = @pass";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@sur", surName);
                        cmd.Parameters.AddWithValue("@pass", password);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch { return false; }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e) => ClearPatientForm();

        private void ClearPatientForm()
        {
            TxtPatientId.Text = string.Empty;
            TxtLastName.Clear(); TxtAge.Clear(); TxtCity.Clear(); TxtDiagnosis.Clear();
            ComboGender.SelectedIndex = 0;

            GroupPatientForm.Header = "Реєстрація нового пацієнта";
            BtnSavePatient.Content = "Додати пацієнта";
            BtnSavePatient.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69));
            BtnCancelEdit.Visibility = Visibility.Collapsed;

            ResetGridSelection();
        }

        private void ResetGridSelection()
        {
            PatientsGrid.SelectionChanged -= PatientsGrid_SelectionChanged;
            PatientsGrid.SelectedItem = null;
            PatientsGrid.SelectionChanged += PatientsGrid_SelectionChanged;
        }

        #endregion

        #region ФІЛЬТРАЦІЯ ТА ПОШУК

        private void SearchPatient_Click(object sender, RoutedEventArgs e)
        {
            string searchName = TxtSearchLastName.Text.Trim();
            if (string.IsNullOrEmpty(searchName)) return;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM patients WHERE `Прізвище` LIKE @search ORDER BY `№` ASC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@search", searchName + "%");
                        DataTable dt = new DataTable();
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd)) { adapter.Fill(dt); }
                        PatientsGrid.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Помилка пошуку: " + ex.Message); }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchLastName.Clear();
            LoadData();
        }

        private void LoadUniqueDiagnoses()
        {
            try
            {
                DiagFilterCombo.Items.Clear();
                DiagFilterCombo.Items.Add("Всі діагнози");
                DiagFilterCombo.SelectedIndex = 0;

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT `Діагноз` FROM patients WHERE `Діагноз` IS NOT NULL AND `Діагноз` != '' ORDER BY `Діагноз`";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) { DiagFilterCombo.Items.Add(reader.GetString("Діагноз")); }
                    }
                }
            }
            catch { }
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int minAge = 0;
                if (!string.IsNullOrEmpty(AgeLimitInput.Text) && !int.TryParse(AgeLimitInput.Text, out minAge))
                {
                    MessageBox.Show("Введіть число для мінімального віку.", "Помилка вводу", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedDiag = DiagFilterCombo.SelectedItem?.ToString();

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM patients WHERE `Вік` >= @MinAge";

                    if (!string.IsNullOrEmpty(selectedDiag) && selectedDiag != "Всі діагнози")
                    {
                        query += " AND `Діагноз` = @Diag";
                    }
                    query += " ORDER BY `№` ASC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@MinAge", minAge);
                        if (!string.IsNullOrEmpty(selectedDiag) && selectedDiag != "Всі діагнози")
                        {
                            cmd.Parameters.AddWithValue("@Diag", selectedDiag);
                        }

                        DataTable dt = new DataTable();
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd)) { adapter.Fill(dt); }
                        PatientsGrid.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Помилка фільтрації: {ex.Message}"); }
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            AgeLimitInput.Clear();
            if (DiagFilterCombo.Items.Count > 0) DiagFilterCombo.SelectedIndex = 0;
            LoadData();
        }

        #endregion

        #region ФОРМУВАННЯ СИНХРОННОГО ЗВІТУ З ПЕРЕГЛЯДОМ

        private void GenerateTxtReport_Click(object sender, RoutedEventArgs e)
        {
            string ageText = AgeLimitInput.Text.Trim();
            string selectedDiag = DiagFilterCombo.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(ageText) || string.IsNullOrWhiteSpace(selectedDiag))
            {
                MessageBox.Show("Помилка: Не всі поля заповнені!\nБудь ласка, вкажіть фільтр віку та оберіть діагноз для формування звіту.", 
                                "Неповні дані фільтрації", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ageText, out int limitAge) || limitAge < 0 || limitAge > 120)
            {
                MessageBox.Show("Будь ласка, введіть коректне числове значення для віку (від 0 до 120).", 
                                "Помилка вводу віку", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                return;
            }

            try
            {
                var dataView = PatientsGrid.ItemsSource as DataView;
                if (dataView == null || dataView.Count == 0)
                {
                    MessageBox.Show("Немає даних для звіту за вказаними критеріями.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string fileName = $"Звіт_Пацієнти_{DateTime.Now:yyyyMMdd_HHmm}.txt";
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                int maxSur = Math.Max("Прізвище".Length, dataView.Cast<DataRowView>().Select(r => r["Прізвище"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());
                int maxCity = Math.Max("Місто".Length, dataView.Cast<DataRowView>().Select(r => r["Місто"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());
                int maxDiag = Math.Max("Діагноз".Length, dataView.Cast<DataRowView>().Select(r => r["Діагноз"]?.ToString()?.Length ?? 0).DefaultIfEmpty(0).Max());

                using (StreamWriter sw = new StreamWriter(path, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("           ЗВІТ СТОМАТОЛОГІЧНОЇ КЛІНІКИ");
                    sw.WriteLine("================================================================");
                    sw.WriteLine($"Критерії збірки: Вік >= {limitAge} | Діагноз: {selectedDiag}");
                    sw.WriteLine("----------------------------------------------------------------");

                    string format = "{0,-3} | {1,-" + maxSur + "} | {2,-2} | {3,-3} | {4,-" + maxCity + "} | {5}";
                    
                    sw.WriteLine(string.Format(format, "№", "Прізвище", "Ст", "Вік", "Місто", "Діагноз"));
                    sw.WriteLine(new string('-', maxSur + maxCity + maxDiag + 25));

                    int countOthers = 0;
                    foreach (DataRowView rowView in dataView)
                    {
                        DataRow row = rowView.Row;
                        if ((row["Місто"]?.ToString() ?? "").Trim().ToLower() != "суми") countOthers++;
                        sw.WriteLine(string.Format(format, row["№"], row["Прізвище"], row["Стать"], row["Вік"], row["Місто"], row["Діагноз"]));
                    }
                    
                    sw.WriteLine("================================================================");
                    sw.WriteLine($"Іногородніх (поза обласним центром): {countOthers}");
                    sw.WriteLine($"Всього пацієнтів у звіті: {dataView.Count}");
                }

                // ПИТАННЯ ПРО ОДРАЗОВИЙ ПЕРЕГЛЯД ЗВІТУ
                MessageBoxResult result = MessageBox.Show(
                    $"Звіт успішно згенеровано та збережено на Робочий стіл:\n{fileName}\n\nБажаєте відкрити та переглянути його прямо зараз?", 
                    "Успіх", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true 
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex) 
            { 
                MessageBox.Show("Помилка формування звіту: " + ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        #endregion

        private void RefreshData_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}