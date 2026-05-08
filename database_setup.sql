USE dental_db;
DROP TABLE IF EXISTS patients;
CREATE TABLE patients (
    id INT AUTO_INCREMENT PRIMARY KEY,
    lastName VARCHAR(100), 
    gender VARCHAR(10),             
    age INT,                        
    city VARCHAR(100),              
    diagnosis VARCHAR(255)          
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;