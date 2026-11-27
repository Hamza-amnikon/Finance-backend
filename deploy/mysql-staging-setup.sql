-- Run as a MySQL DBA on 172.31.4.3. This does not modify SparkFinance.
-- Clone SparkFinance into SparkFinanceStaging with the DBA's backup/restore tool
-- before running financial POST tests. Keep the clone on the same restricted host.
CREATE DATABASE IF NOT EXISTS `SparkFinanceStaging`
    CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, CREATE VIEW
    ON `SparkFinanceStaging`.* TO 'sa'@'172.31.3.11';
