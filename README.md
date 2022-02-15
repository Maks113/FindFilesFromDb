# FilesFinder

Утилита для проверки наличия файлов по пути сохраненному в бд MSSQL.

Настраивается в appsettings.json

```json5
{
  "DatabaseConfig": {
    // Имя сервера БД
    "ServerName": "",
    // Пользователь
    "User": "maks113",
    // Пароль
    "Password": "zcsqzcsq",
    // Имя базы даннх
    "DatabaseName": "db_tm_mini"
  },
  "FinderConfiguration": {
    // Целевые таблицы и поля
    "Targets": [
      {
        // Название таблицы
        "TableName": "tbl_contract",
        // Поле, содержащее путь к файлу
        "PathField": "[doc_path]",
        // Поле с id этой таблицы
        "IdField": "[ID]",
        // дополнительные поля для включения в лог
        "InfoFields": [
          {
            // Имя поля
            "FieldName": "[NUM_OF]",
            // подсказка для лога
            "Description": "номер контракта"
          }
        ]
      }
    ]
  }
}
```

В результате выполнения для каждого из объектов в target в директории Results создаются:
 - лог всех файлов с результатом
 - лог только найденных файлов
 - лог только не найденных файлов
 + csv файл с не найденными файлами и дополнительными полями
 + таблица del_FileSearchResults_<имя таблицы TableName>_<имя поля с файлом PathField>