# ConfigManager
Light configuration manager that supports loading into different targets: Class / Special dictionary

# Installing library
...

# Getting started
Library provides basic class ```Config``` for working with data.
Supported features:
  - Loading from ```String / File```
  - Loading into ```Class / ConfigValue tree```

Example of [configuration file](#)

# Writing your first configuration

Let's look at a simple file
```
value yes
value no
id 0
inheritance
  separated "by space"

empty_line "are counted"
  parents "can hold data too"
  data "can have tabs:\tand new lines:\n"
```
It shows what you can do within configuration.
Each line consists of several parts:
  - ```indentation``` - for marking enclosed values
  - ```key``` - value identifier. If repeated it's values will be stored as an array
  - ```data``` - the rest of line with data separated by a whitespace

For accessing that data using ConfigManager [special methods](#) are used.

# Implementing configuration in your project
On [wiki pages](#) you might find everything you need to start working with configuration manager