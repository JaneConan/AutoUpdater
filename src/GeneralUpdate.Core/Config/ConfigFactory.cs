﻿using GeneralUpdate.Common.Utils;
using GeneralUpdate.Core.Config.Cache;
using GeneralUpdate.Core.Config.Handles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Config
{
    /// <summary>
    /// Update local configuration file.[Currently only files with a depth of 1 are supported.]
    /// Currently only json files are supported.
    /// </summary>
    public sealed class ConfigFactory
    {
        #region Private Members

        private static readonly object _lock = new object();
        private static ConfigFactory _instance = null;
        private List<string> FileTypes = new List<string> { ".db", ".xml", ".ini", ".json" };
        private const string FolderName = "backup";
        private ConfigCache<ConfigEntity> _configCache;
        private string _tempBackupPath;
        private string _targetPath;
        private List<string> _files;
        private bool _disposed = false;

        #endregion Private Members

        #region Constructors

        private ConfigFactory()
        {
            _configCache = new ConfigCache<ConfigEntity>();
            _targetPath = _targetPath ?? Environment.CurrentDirectory;
            _tempBackupPath = _tempBackupPath ?? Path.Combine(_targetPath, $"{ FolderName }_{ DateTime.Now.ToShortTimeString() }");
        }

        ~ConfigFactory()
        {
            Dispose();
        }

        #endregion Constructors

        #region Public Properties

        public static ConfigFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigFactory();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Deploy configuration file.
        /// </summary>
        public async Task Deploy()
        {
            try
            {
                if (_configCache.Cache != null)
                {
                    foreach (var cacheItem in _configCache.Cache)
                    {
                        var value = cacheItem.Value;
                        if (value == null) continue;
                        var handle = InitHandle<ConfigEntity>(value.Handle);
                        await handle.Write(value.Path, value);
                    }
                    Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Deploy config error : { ex.Message } .", ex.InnerException);
            }
        }

        /// <summary>
        /// Scan configuration files and cache, backup.
        /// </summary>
        public async Task Scan()
        {
            try
            {
                List<string> files = new List<string>();
                TreeUtil.Find(_targetPath, ref files);
                if (files.Count == 0) return;
                _files = files;
                await Cache(_files);
            }
            catch (Exception ex)
            {
                throw new Exception($"Scan config files error : { ex.Message } .", ex.InnerException);
            }
        }

        /// <summary>
        /// release all resources.
        /// </summary>
        /// <exception cref="Exception">dispose exception</exception>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (Directory.Exists(_tempBackupPath)) Directory.Delete(_tempBackupPath, true);

                if (_configCache != null)
                {
                    _configCache.Dispose();
                    _configCache = null;
                }

                if (_files != null)
                {
                    _files.Clear();
                    _files = null;
                }

                if (_instance != null) _instance = null;

                _disposed = true;
            }
            catch (Exception ex)
            {
                _disposed = false;
                throw new Exception($"'Dispose' error :{ ex.Message } .", ex.InnerException);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// All resources are cached and backed up.
        /// </summary>
        /// <param name="files"></param>
        /// <exception cref="Exception"></exception>
        private async Task Cache(IEnumerable<string> files)
        {
            if (_files == null) return;
            try
            {
                foreach (var file in files)
                {
                    File.Copy(file, _tempBackupPath);
                    var fileMD5 = FileUtil.GetFileMD5(file);
                    var entity = await Handle(file, fileMD5);
                    _configCache.TryAdd(fileMD5, entity);
                }
                _configCache.Build();
            }
            catch (Exception ex)
            {
                throw new Exception($"'Cache' error :{ ex.Message } .", ex.InnerException);
            }
        }

        /// <summary>
        /// Process file content.
        /// </summary>
        /// <param name="file">file path</param>
        /// <param name="fileMD5">md5</param>
        /// <returns></returns>
        private async Task<ConfigEntity> Handle(string file, string fileMD5)
        {
            var entity = new ConfigEntity();
            entity.Path = file;
            entity.MD5 = fileMD5;
            entity.Handle = ToEnum(file);
            entity.Content = await InitHandle<object>(entity.Handle).Read(file);
            return entity;
        }

        /// <summary>
        /// Initialize the corresponding file processing object.
        /// </summary>
        /// <typeparam name="TEntity">file entity</typeparam>
        /// <param name="handleEnum">handle enum</param>
        /// <returns>handle</returns>
        private IHandle<TEntity> InitHandle<TEntity>(HandleEnum handleEnum) where TEntity : class
        {
            IHandle<TEntity> handle = null;
            switch (handleEnum)
            {
                case HandleEnum.DB:
                    handle = new DBHandle<TEntity>();
                    break;

                case HandleEnum.INI:
                    handle = new IniHandle<TEntity>();
                    break;

                case HandleEnum.JSON:
                    handle = new JsonHandle<TEntity>();
                    break;

                case HandleEnum.XML:
                    handle = new XmlHandle<TEntity>();
                    break;
            }
            return handle;
        }

        /// <summary>
        /// Convert enumeration value according to file type.
        /// </summary>
        /// <param name="file">file path</param>
        /// <returns>handle enum</returns>
        private HandleEnum ToEnum(string file)
        {
            var fileExtension = Path.GetExtension(file);
            var handleEnum = HandleEnum.NONE;
            switch (fileExtension)
            {
                case ".db":
                    handleEnum = HandleEnum.DB;
                    break;

                case ".ini":
                    handleEnum = HandleEnum.INI;
                    break;

                case ".json":
                    handleEnum = HandleEnum.JSON;
                    break;

                case ".xml":
                    handleEnum = HandleEnum.XML;
                    break;
            }
            return handleEnum;
        }

        #endregion Private Methods
    }
}