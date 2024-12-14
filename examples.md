CREATE TABLE Persons (PersonID int PRIMARY KEY, name varchar, samplenumeric int);
INSERT into Persons (PersonID, name, samplenumeric) VALUES (1, 'Bella', 555), (2, 'Charlie', 666), (3, 'Lucy', 777), (4, 'Luna', 888);
SELECT * FROM PERSONS WHERE samplenumeric % 111 = 0;

CREATE TABLE products (
    id INT PRIMARY KEY,
    name VARCHAR,
    category VARCHAR,
    price DECIMAL
);

INSERT INTO products (id, name, category, price) VALUES (1, 'Laptop', 'Electronics', 999.99), (2, 'Smartphone', 'Electronics', 699.99), (3, 'Headphones', 'Electronics', 199.99), (4, 'Coffee Maker', 'Appliances', 79.99), (5, 'Toaster', 'Appliances', 49.99), (6, 'Blender', 'Appliances', 89.99), (7, 'Smart Watch', 'Electronics', 299.99), (8, 'Microwave', 'Appliances', 149.99);

CREATE TABLE users (
    id INT PRIMARY KEY,
    name VARCHAR,
    email VARCHAR,
    created_at DATETIME
);

INSERT INTO users (id, name, email, created_at) VALUES (1, 'John Doe', 'john@example.com', '2024-01-01');
INSERT INTO users (id, name, email, created_at) VALUES (2, 'Jane Smith', 'jane@example.com', '2024-01-02');
INSERT INTO users (id, name, email, created_at) VALUES (3, 'Bob Wilson', 'bob@example.com', '2024-01-03');
INSERT INTO users (id, name, email, created_at) VALUES (4, 'Alice Brown', 'alice@example.com', '2024-01-04');
INSERT INTO users (id, name, email, created_at) VALUES (5, 'Charlie Smith', 'charlie@example.com', '2024-01-05');
INSERT INTO users (id, name, email, created_at) VALUES (6, 'Charlie Smith', 'charlie2@example.com', '2024-01-05');

SELECT COUNT(*) FROM users;
SELECT name, COUNT(*) FROM users GROUP BY name;
SELECT name, MAX(created_at) FROM users GROUP BY name;

SELECT name, COUNT(*) 
FROM users 
WHERE name LIKE '%Smith'
GROUP BY name;

SELECT name, COUNT(*) 
FROM users 
WHERE created_at >= '2024-01-02'
GROUP BY name 
HAVING COUNT(*) > 1;

SELECT u.id, u.name FROM users u;
SELECT u.id, u.name FROM users AS u;
SELECT COUNT(*) AS total_users, AVG(id) AS avg_id FROM users;
