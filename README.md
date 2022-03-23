# FilesFinder

Утилита для проверки наличия файлов по пути сохраненному в бд MSSQL.

Настраивается в appsettings.json

```json5
{
  "UserConfiguration": {
    // Id пользователя, от имени которого будут добавляться файлы
    "Id": 1
  },
  "DatabaseConfiguration": {
    // Имя сервера БД
    "ServerName": "",
    // Пользователь
    "User": "login",
    // Пароль
    "Password": "password",
    // Имя базы даннх
    "DatabaseName": "db_tm_mini"
  },
  "FinderConfiguration": {
    // Целевые таблицы и поля
    "Targets": [
      {
        // Идентификатор хранилища для добавления файлов из этой таблицы (FilesetsConfiguration.Filesets[0].Id) 
        "FilesetId": "documents",
        // Имя поля, в которое будет записываться filesetId
        "FilesetFieldName": "fileset_id",
        // Название таблицы
        "TableName": "contract",
        // Поле, содержащее путь к файлу
        "PathField": "path",
        // Поле с id этой таблицы
        "IdField": "ID",
        // дополнительные поля для включения в лог
        "InfoFields": [
          {
            // Имя поля
            "FieldName": "NUM_OF",
            // подсказка для лога
            "Description": "номер контракта"
          }
        ]
      }
    ]
  },
  "FilesetsConfiguration": {
    "Filesets": [
      {
        // Идентификатор хранилища
        "Id": "documents",
        // Имя таблицы для хранения информации о файлах
        "TableName": "tbl_documents_fileset",
        // Базовый путь к хранилищу
        "Host": "\\\\192.168.1.1",
        // Дополнительный (создаваемый) путь
        "StaticPath": "\\documents"
      }
    ]
  },
}
```

В результате выполнения для каждого из объектов в target в директории Results создаются:
 - лог всех файлов с результатом
 - лог только найденных файлов
 - лог только не найденных файлов
 + csv файл с не найденными файлами и дополнительными полями
 + таблица del_FileSearchResults_<имя таблицы TableName>_<имя поля с файлом PathField>